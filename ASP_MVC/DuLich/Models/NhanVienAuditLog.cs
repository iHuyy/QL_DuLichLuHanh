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

        [Required]
        [Column("LOAI_HANH_DONG")]
        [StringLength(10)]
        public string LoaiHanhDong { get; set; }

        [Column("TEN_COT")]
        [StringLength(30)]
        public string? TenCot { get; set; }

        [Column("GIA_TRI_CU")]
        [StringLength(4000)]
        public string? GiaTriCu { get; set; }

        [Column("GIA_TRI_MOI")]
        [StringLength(4000)]
        public string? GiaTriMoi { get; set; }

        [Column("NGUOI_THUC_HIEN")]
        [StringLength(100)]
        public string? NguoiThucHien { get; set; }

        [Column("THOI_GIAN_THUC_HIEN", TypeName = "TIMESTAMP")]
        public DateTime ThoiGianThucHien { get; set; }

        [Column("DINH_DANH_KHACH_HANG")]
        [StringLength(64)]
        public string? DinhDanhKhachHang { get; set; }
    }
}