using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DuLich.Models
{
    [Table("DANHGIATOUR", Schema = "TADMIN")]
    public class DanhGiaTour
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("MADANHGIA")]
        public int MaDanhGia { get; set; }

        [Column("MAKHACHHANG")]
        public int MaKhachHang { get; set; }

        [Column("MATOUR")]
        public int MaTour { get; set; }

        [Column("SOSAO")]
        public int? SoSao { get; set; }

        [Column("NOIDUNGDANHGIA")]
        public string? NoiDungDanhGia { get; set; }

        [Column("NGAYDANHGIA")]
        public DateTime? NgayDanhGia { get; set; }

        [ForeignKey("MaKhachHang")]
        public KhachHang? KhachHang { get; set; }

        [ForeignKey("MaTour")]
        public Tour? Tour { get; set; }
    }
}