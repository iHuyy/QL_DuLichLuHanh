using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DuLich.Models
{
    // Use uppercase names so EF/Oracle provider doesn't emit quoted mixed-case identifiers
    [Table("KHACHHANG", Schema = "tAdmin")]
    public class KhachHang
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("MAKHACHHANG")]
        public int MaKhachHang { get; set; }

        [Column("HOTEN")]
        public string? HoTen { get; set; }

        [Column("EMAIL")]
        public string Email { get; set; } = null!;

        [Column("SODIENTHOAI")]
        public string? SoDienThoai { get; set; }

        [Column("DIACHI")]
        public string? DiaChi { get; set; }

        [Column("VAITRO")]
        public string VaiTro { get; set; } = "KhachHang"; // Admin or KhachHang

        [Column("TRANGTHAI")]
        public string TrangThai { get; set; } = "HoatDong"; // HoatDong or BiKhoa

        [Column("NGAYTAO")]
        public DateTime? NgayTao { get; set; }

        [Column("ORACLE_USERNAME")]
        public string? ORACLE_USERNAME { get; set; }
    }
}
