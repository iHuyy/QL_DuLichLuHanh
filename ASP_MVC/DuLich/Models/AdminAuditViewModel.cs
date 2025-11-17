using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace DuLich.Models
{
    public class AdminAuditViewModel
    {
        public List<AuditRecord> TriggerRecords { get; set; } = new();
        public List<AuditRecord> StandardRecords { get; set; } = new();
        public List<AuditRecord> FgaRecords { get; set; } = new();
        public string ActiveTab { get; set; } = "TRIGGER";
        public int PageSize { get; set; } = 20;
        public int TriggerPage { get; set; } = 1;
        public int StandardPage { get; set; } = 1;
        public int FgaPage { get; set; } = 1;
        public int TriggerTotal { get; set; }
        public int StandardTotal { get; set; }
        public int FgaTotal { get; set; }
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
    }
}
