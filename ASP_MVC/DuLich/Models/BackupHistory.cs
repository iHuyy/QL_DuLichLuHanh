using System;

namespace DuLich.Models
{
    public class BackupHistory
    {
        public int Id { get; set; }
        public string ActionType { get; set; } = string.Empty; // Full / Incremental / Restore
        public DateTime RequestedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; } = "Đang chạy"; // Đang chạy / Hoàn tất / Thất bại
        public string? Target { get; set; } // Đường dẫn backupset hoặc checkpoint
        public string? Notes { get; set; }
        public string? RequestedBy { get; set; }
    }
}
