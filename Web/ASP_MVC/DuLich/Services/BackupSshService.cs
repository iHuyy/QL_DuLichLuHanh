using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Renci.SshNet;

namespace DuLich.Services
{
    public class BackupSshService
    {
        private readonly IConfiguration _configuration;

        public BackupSshService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public List<BackupInfo> ListBackups()
        {
            var cfg = _configuration.GetSection("BackupSsh");
            var listScript = cfg["ListBackupsScript"] ?? "/u01/app/oracle/list_backups.sh";

            var result = ExecuteSshCommand(listScript);
            if (result.ExitStatus != 0)
            {
                throw new InvalidOperationException($"Failed to list backups. SSH Exit Status: {result.ExitStatus}. Output: {result.Output}");
            }

            return ParseBackupList(result.Output);
        }

        public BackupExecutionResult RunBackup(string type)
        {
            var cfg = _configuration.GetSection("BackupSsh");
            var backupScript = cfg["BackupScript"] ?? "/u01/app/oracle/run_backup.sh";

            var backupParam = type?.ToUpper() switch
            {
                "INCREMENTAL" => "--incremental",
                _ => "--full"
            };

            var commandText = $"{backupScript} {backupParam}";
            var result = ExecuteSshCommand(commandText);
            result.BackupPath = ParseBackupDirectoryPath(result.Output);
            return result;
        }

        public BackupExecutionResult RunRestoreFromDirectory(string backupDirectory, string? untilTime = null)
        {
            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                throw new ArgumentException("Backup directory path cannot be empty.", nameof(backupDirectory));
            }

            var cfg = _configuration.GetSection("BackupSsh");
            var restoreScript = cfg["RestoreScript"] ?? "/u01/app/oracle/restore_from_path.sh";

            var formattedUntil = string.IsNullOrWhiteSpace(untilTime)
                ? string.Empty
                : DateTime.Parse(untilTime).ToString("yyyy-MM-dd HH:mm:ss");

            var escapedUntil = string.IsNullOrWhiteSpace(formattedUntil)
                ? string.Empty
                : formattedUntil.Replace("\"", "\\\"");
            var escapedDir = backupDirectory.Replace("\"", "\\\"");

            var untilPrefix = string.IsNullOrWhiteSpace(escapedUntil)
                ? string.Empty
                : $"UNTIL_TIME=\"{escapedUntil}\" ";

            var commandText = $"{untilPrefix}{restoreScript} \"{escapedDir}\" ";
            return ExecuteSshCommand(commandText);
        }

        private BackupExecutionResult ExecuteSshCommand(string commandText)
        {
            var cfg = _configuration.GetSection("BackupSsh");
            var host = cfg["Host"];
            var sshPort = int.TryParse(cfg["SshPort"], out var port) ? port : 22;
            var username = cfg["Username"];
            var password = cfg["Password"];

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username))
            {
                throw new InvalidOperationException("Backup SSH configuration is missing Host/Username.");
            }

            using var client = new SshClient(host, port, username, password);
            try
            {
                client.Connect();
                if (!client.IsConnected)
                {
                    throw new Exception("SSH connection failed.");
                }

                var cmd = client.CreateCommand(commandText);
                cmd.CommandTimeout = TimeSpan.FromMinutes(30);
                var resultText = cmd.Execute();
                var error = cmd.Error;

                var output = $"STDOUT:\n{resultText}\n\nSTDERR:\n{error}";

                client.Disconnect();

                return new BackupExecutionResult
                {
                    ExitStatus = cmd.ExitStatus,
                    Output = output,
                    BackupPath = "" // Will be parsed by the calling method if needed
                };
            }
            catch (Exception ex)
            {
                return new BackupExecutionResult
                {
                    ExitStatus = -1,
                    Output = ex.ToString(),
                    BackupPath = string.Empty
                };
            }
        }

        private static List<BackupInfo> ParseBackupList(string scriptOutput)
        {
            var backups = new List<BackupInfo>();
            if (string.IsNullOrWhiteSpace(scriptOutput)) return backups;

            var matches = Regex.Matches(scriptOutput, @"---BACKUPSET_START---(?<content>.*?)---BACKUPSET_END---", RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                var content = match.Groups["content"].Value.Trim();
                // Corrected Regex
                var pathMatch = Regex.Match(content, @"^Path:\s*(?<path>.*)$", RegexOptions.Multiline);
                var timestampStringMatch = Regex.Match(content, @"^Timestamp:\s*(?<timestamp>.*)$", RegexOptions.Multiline);

                if (pathMatch.Success && timestampStringMatch.Success)
                {
                    var timestampStr = timestampStringMatch.Groups["timestamp"].Value.Trim();
                    DateTime parsedTimestamp;
                    // Attempt to parse the timestamp string. Use a common format or try multiple.
                    if (!DateTime.TryParse(timestampStr, out parsedTimestamp))
                    {
                        // Fallback to a default or throw an error if parsing fails
                        // For now, setting to a min value if parsing fails
                        parsedTimestamp = DateTime.MinValue;
                    }

                    backups.Add(new BackupInfo
                    {
                        Path = pathMatch.Groups["path"].Value.Trim(),
                        TimestampString = timestampStr,
                        Timestamp = parsedTimestamp
                    });
                }
            }

            return backups;
        }

        private static string ParseBackupDirectoryPath(string scriptOutput)
        {
            if (string.IsNullOrWhiteSpace(scriptOutput)) return string.Empty;

            try
            {
                var lines = scriptOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var pathLine = lines.FirstOrDefault(l => l.Trim().StartsWith("BACKUP_PATH="));

                if (pathLine != null)
                {
                    return pathLine.Trim().Substring("BACKUP_PATH=".Length);
                }
            }
            catch
            {
                // Ignore parsing errors
            }

            return string.Empty;
        }
    }

    public class BackupInfo
    {
        public string Path { get; set; } = string.Empty;
        public string TimestampString { get; set; } = string.Empty; // Keep original string for display
        public DateTime Timestamp { get; set; } // Parsed DateTime for sorting and logic
    }

    public class BackupExecutionResult
    {
        public int ExitStatus { get; set; }
        public string Output { get; set; } = string.Empty;
        public string BackupPath { get; set; } = string.Empty;
    }
}