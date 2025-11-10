using System.Collections.Generic;

namespace DuLich.Models
{
    public class BackupRestoreViewModel
    {
        public string? BackupFileName { get; set; }
        public string? Message { get; set; }
        public bool IsSuccess { get; set; }
        public List<string> BackupFiles { get; set; }

        public BackupRestoreViewModel()
        {
            BackupFiles = new List<string>();
        }
    }
}
