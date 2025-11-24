using System;
using Microsoft.Extensions.Configuration;
using Renci.SshNet;
using System.Linq;

namespace DuLich.Services
{
    public class BackupSshService
    {
        private readonly IConfiguration _configuration;

        public BackupSshService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public BackupExecutionResult RunBackup(string type)
        {
            var cfg = _configuration.GetSection("BackupSsh");
            var backupScript = cfg["BackupScript"] ?? "/u01/app/oracle/run_backup.sh"; // Default path

            var backupParam = type?.ToUpper() switch
            {
                "INCREMENTAL" => "--incremental",
                _ => "--full"
            };

            var commandText = $"{backupScript} {backupParam}";
            return ExecuteSshCommand(commandText);
        }

        public BackupExecutionResult RunRestoreFromDirectory(string backupDirectory, string? untilTime = null)
        {
            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                throw new ArgumentException("Backup directory path cannot be empty.", nameof(backupDirectory));
            }

            var cfg = _configuration.GetSection("BackupSsh");
            var restoreScript = cfg["RestoreScript"] ?? "/u01/app/oracle/restore_from_path.sh"; // Default path

            // Pass the directory path as an argument to the script, optionally with UNTIL_TIME for point-in-time restore.
            // Format UNTIL_TIME to a consistent Oracle-friendly string.
            var formattedUntil = string.IsNullOrWhiteSpace(untilTime)
                ? string.Empty
                : DateTime.Parse(untilTime).ToString("yyyy-MM-dd HH:mm:ss");

            var escapedUntil = string.IsNullOrWhiteSpace(formattedUntil)
                ? string.Empty
                : formattedUntil.Replace("'", "'\"'\"'");
            var escapedDir = backupDirectory.Replace("'", "'\"'\"'");

            var untilPrefix = string.IsNullOrWhiteSpace(escapedUntil)
                ? string.Empty
                : $"UNTIL_TIME='{escapedUntil}' ";

            var commandText = $"{untilPrefix}{restoreScript} '{escapedDir}'";
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
                var result = cmd.Execute();
                var error = cmd.Error;
                var output = string.IsNullOrEmpty(result) ? error : result;

                client.Disconnect();

                return new BackupExecutionResult
                {
                    ExitStatus = cmd.ExitStatus,
                    Output = output,
                    BackupPath = ParseBackupPath(output) // Keep parsing the output
                };
            }
            catch (Exception ex)
            {
                return new BackupExecutionResult
                {
                    ExitStatus = -1,
                    Output = ex.Message,
                    BackupPath = string.Empty
                };
            }
        }
        
        // This method now expects the .sh script to output a line like:
        // BACKUP_PATH=/path/to/backup.bkp
        private static string ParseBackupPath(string scriptOutput)
        {
            if (string.IsNullOrWhiteSpace(scriptOutput)) return string.Empty;

            try
            {
                var line = scriptOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(l => l.Trim().StartsWith("BACKUP_PATH="));

                if (line != null)
                {
                    return line.Trim().Substring("BACKUP_PATH=".Length);
                }
            }
            catch
            {
                // Ignore parsing errors
            }
            
            return string.Empty;
        }
    }

    public class BackupExecutionResult
    {
        public int ExitStatus { get; set; }
        public string Output { get; set; } = string.Empty;
        public string BackupPath { get; set; } = string.Empty;
    }
}
