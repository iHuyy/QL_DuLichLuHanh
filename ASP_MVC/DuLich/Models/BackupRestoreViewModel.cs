using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DuLich.Models
{
    public class BackupRestoreViewModel
    {
        public string RetentionPolicy { get; set; } = "CONFIGURE RETENTION POLICY TO REDUNDANCY 2;";
        public string ControlfileAutobackup { get; set; } = "CONFIGURE CONTROLFILE AUTOBACKUP ON;";
        public string ChannelFormat { get; set; } = "CONFIGURE CHANNEL DEVICE TYPE DISK FORMAT '/u01/backup/%d_%T_%U.bkp';";
        public string ArchivelogDeletionPolicy { get; set; } = "CONFIGURE ARCHIVELOG DELETION POLICY TO BACKED UP 2 TIMES TO DISK;";
        public List<BackupHistoryItem> History { get; set; } = new();
        public List<BackupHistoryItem> RestoreHistory { get; set; } = new();
        public List<SelectListItem> BackupFiles { get; set; } = new();
        public List<SelectListItem> BackupDirectories { get; set; } = new();
    }

    public class BackupHistoryItem
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty; // Full / Incremental
        public DateTime StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public string Status { get; set; } = string.Empty; // e.g. Completed, Running, Failed
        public string Location { get; set; } = string.Empty; // backupset path or FRA
        public string Note { get; set; } = string.Empty;
    }
}
