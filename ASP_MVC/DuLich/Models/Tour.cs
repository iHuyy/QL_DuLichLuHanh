using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DuLich.Models
{
    [Table("TOUR", Schema = "TADMIN")]
    public class Tour
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("MATOUR")]
        public int MaTour { get; set; }

        [Column("TIEUDE")]
        public string? TieuDe { get; set; }

        [Column("MOTA")]
        public string? MoTa { get; set; }

        [Column("NOIKHOIHANH")]
        public string? NoiKhoiHanh { get; set; }

        [Column("NOIDEN")]
        public string? NoiDen { get; set; }

        [Column("THANHPHO")]
        public string? ThanhPho { get; set; }

        [Column("THOIGIAN")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? ThoiGian { get; set; }

        [Column("GIANGUOILON")]
        public decimal? GiaNguoiLon { get; set; }

        [Column("GIATREEM")]
        public decimal? GiaTreEm { get; set; }

        [Column("TRANGTHAI")]
        public string? TrangThai { get; set; }

        [Column("SOLUONG")]
        public int? SoLuong { get; set; }

        [Column("QR")]
        public string? QR { get; set; }
    }
}
