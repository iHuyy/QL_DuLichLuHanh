using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DuLich.Models
{
    [Table("HOADON", Schema = "TADMIN")]
    public class HoaDon
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("MAHOADON")]
        public int MaHoaDon { get; set; }

        [Column("MADATTOUR")]
        public int? MaDatTour { get; set; }

        [Column("SOTIEN")]
        public decimal? SoTien { get; set; }

        [Column("NGAYXUAT")]
        public DateTime? NgayXuat { get; set; }

        [Column("TRANGTHAI")]
        public string? TrangThai { get; set; }

        [Column("CHUKYSO")]
        public string? ChuKySo { get; set; }

        [ForeignKey("MaDatTour")]
        public DatTour? DatTour { get; set; }

        
    }
}