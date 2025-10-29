using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DuLich.Models
{
    [Table("DATTOUR", Schema = "TADMIN")]
    public class DatTour
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("MADATTOUR")]
        public int MaDatTour { get; set; }

        [Column("MAKHACHHANG")]
        public int? MaKhachHang { get; set; }

        [Column("MATOUR")]
        public int? MaTour { get; set; }

        [Column("NGAYDAT")]
        public DateTime? NgayDat { get; set; }

        [Column("SONGUOILON")]
        public int? SoNguoiLon { get; set; }

        [Column("SOTREEM")]
        public int? SoTreEm { get; set; }

        [Column("TONGTIEN")]
        public decimal? TongTien { get; set; }

        [Column("TRANGTHAITHANHTOAN")]
        public string? TrangThaiThanhToan { get; set; }

        [Column("TRANGTHAIDAT")]
        public string? TrangThaiDat { get; set; }

        [Column("YEUCAUDACBIET")]
        public string? YeuCauDacBiet { get; set; }

        [ForeignKey("MaKhachHang")]
        public KhachHang? KhachHang { get; set; }

        [ForeignKey("MaTour")]
        public Tour? Tour { get; set; }
    }
}