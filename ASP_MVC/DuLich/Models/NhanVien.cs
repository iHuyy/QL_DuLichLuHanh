using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DuLich.Models
{
    [Table("NHANVIEN", Schema = "TADMIN")]
    public class NhanVien
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("MANHANVIEN")]
        public int MaNhanVien { get; set; }

        [Column("HOTEN")]
        public string? HoTen { get; set; }

        [Column("EMAIL")]
        public string Email { get; set; } = string.Empty;

        [Column("SODIENTHOAI")]
        public string? SoDienThoai { get; set; }

        [Column("VAITRO")]
        public string VaiTro { get; set; } = "NhanVien";

        [Column("TRANGTHAI")]
        public string TrangThai { get; set; } = "HoatDong";

        [Column("NGAYTAO")]
        public DateTime? NgayTao { get; set; }

        [Column("ORACLE_USERNAME")]
        public string? ORACLE_USERNAME { get; set; }

        // Branch code used for VPD filtering
        [Column("CHINHANH")]
        public string? ChiNhanh { get; set; }
    }
}
