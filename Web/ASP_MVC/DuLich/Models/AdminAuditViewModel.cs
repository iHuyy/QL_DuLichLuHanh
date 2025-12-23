using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace DuLich.Models
{
    public class AdminAuditViewModel
    {
        public List<AuditRecord> TourRecords { get; set; } = new();
        public List<AuditRecord> StaffRecords { get; set; } = new();
        public List<AuditRecord> DatabaseRecords { get; set; } = new();
        public string ActiveTab { get; set; } = "TOUR";
        public int PageSize { get; set; } = 20;
        public int TourPage { get; set; } = 1;
        public int StaffPage { get; set; } = 1;
        public int DatabasePage { get; set; } = 1;
        public int TourTotal { get; set; }
        public int StaffTotal { get; set; }
        public int DatabaseTotal { get; set; }
        public List<BackupHistory> BackupHistories { get; set; } = new();
    }

    public class AuditRecord
    {
        public string Method { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Column { get; set; } = string.Empty;
        public string OldValue { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
        public string PerformedBy { get; set; } = string.Empty;
        public DateTime? Timestamp { get; set; }
        public string? SqlText { get; set; }
        public string? PolicyName { get; set; }
    }
}
