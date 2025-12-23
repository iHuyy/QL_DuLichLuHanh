using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DuLich.Models
{
    [Table("NHANVIEN_AUDIT_LOG", Schema = "TADMIN")]
    public class NhanVienAuditLog
    {
        [Key]
        [Column("MA_KIEM_TOAN")]
        public int MaKiemToan { get; set; }

        [Column("LOAI_HANH_DONG")]
        public string? LoaiHanhDong { get; set; }

        [Column("TEN_COT")]
        public string? TenCot { get; set; }

        [Column("TEN_BANG")]
        public string? TenBang { get; set; }

        [Column("GIA_TRI_CU")]
        public string? GiaTriCu { get; set; }

        [Column("GIA_TRI_MOI")]
        public string? GiaTriMoi { get; set; }

        [Column("NGUOI_THUC_HIEN")]
        public string? NguoiThucHien { get; set; }

        [Column("THOI_GIAN_THUC_HIEN")]
        public DateTime? ThoiGianThucHien { get; set; }

    }
}
