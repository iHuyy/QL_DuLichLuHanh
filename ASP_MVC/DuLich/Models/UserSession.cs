using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DuLich.Models
{
    [Table("USER_SESSIONS", Schema = "TADMIN")]
    public class UserSession
    {
        [Key]
        [Column("SESSION_ID")]
        public string SessionId { get; set; } = null!; // store GUID as string (no dashes)

        [Column("USER_ID")]
        public int UserId { get; set; }

        [Column("USER_TYPE")]
        public string UserType { get; set; } = null!; // 'KhachHang' or 'NhanVien'

        [Column("DEVICE_TYPE")]
        public string DeviceType { get; set; } = "WEB"; // 'WEB' or 'MOBILE'

        [Column("LOGIN_TIME")]
        public DateTime LoginTime { get; set; } = DateTime.UtcNow;

        [Column("LAST_ACTIVITY")]
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;

        [Column("IP_ADDRESS")]
        public string? IpAddress { get; set; }

        [Column("DEVICE_INFO")]
        public string? DeviceInfo { get; set; }

        [Column("IS_ACTIVE")]
        public string IsActive { get; set; } = "Y"; // 'Y' or 'N'
    }
}
