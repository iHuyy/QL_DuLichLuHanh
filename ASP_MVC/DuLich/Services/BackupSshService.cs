using System;
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

        public BackupExecutionResult RunBackup(string type)
        {
            var cfg = _configuration.GetSection("BackupSsh");
            var host = cfg["Host"] ?? string.Empty;
            var sshPort = int.TryParse(cfg["SshPort"], out var port) ? port : 22;
            var username = cfg["Username"] ?? string.Empty;
            var password = cfg["Password"] ?? string.Empty;
            var backupScript = cfg["BackupScript"] ?? "/u01/backup/run_backup.sh";

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username))
            {
                throw new InvalidOperationException("Backup SSH configuration is missing host/username.");
            }

            var backupParam = type?.ToUpper() switch
            {
                "INCREMENTAL" => "--incremental",
                _ => "--full"
            };

            var commandText = $"{backupScript} {backupParam}";
            var result = ExecuteCommand(host, sshPort, username, password, commandText);
            return result;
        }

        public BackupExecutionResult RunRestore(string targetPath)
        {
            var cfg = _configuration.GetSection("BackupSsh");
            var host = cfg["Host"] ?? string.Empty;
            var sshPort = int.TryParse(cfg["SshPort"], out var port) ? port : 22;
            var username = cfg["Username"] ?? string.Empty;
            var password = cfg["Password"] ?? string.Empty;
            var restoreScript = cfg["RestoreScript"] ?? "/u01/backup/restore_from_path.sh";

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username))
            {
                throw new InvalidOperationException("Backup SSH configuration is missing host/username.");
            }

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                throw new ArgumentException("Đường dẫn backup để phục hồi đang trống.", nameof(targetPath));
            }

            var commandText = $"{restoreScript} \"{targetPath}\"";
            var result = ExecuteCommand(host, sshPort, username, password, commandText);
            return result;
        }

        private BackupExecutionResult ExecuteCommand(string host, int port, string username, string password, string commandText)
        {
            using var client = new SshClient(host, port, username, password);
            client.Connect();
            var cmd = client.RunCommand(commandText);
            client.Disconnect();

            var backupPath = ParseBackupPath(cmd.Result);
            return new BackupExecutionResult
            {
                ExitStatus = cmd.ExitStatus,
                Output = cmd.Result,
                BackupPath = backupPath
            };
        }

        private static string ParseBackupPath(string output)
        {
            // Expect script to echo line: BACKUP_PATH=/u01/backup/filename.bkp
            if (string.IsNullOrWhiteSpace(output)) return string.Empty;
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("BACKUP_PATH=", StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed.Substring("BACKUP_PATH=".Length);
                }
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
