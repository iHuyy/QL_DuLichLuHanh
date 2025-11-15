using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DuLich.Models
{
    [Table("ANHTOUR", Schema = "TADMIN")]
    public class AnhTour
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("MAANH")]
        public int MaAnh { get; set; }

        [Column("MATOUR")]
        public int MaTour { get; set; }

        [Column("DULIEUANH")]
        public byte[]? DuLieuAnh { get; set; }

        [Column("LOAIANH")]
        public string? LoaiAnh { get; set; }

        [Column("MOTA")]
        public string? MoTa { get; set; }

        [Column("NGAYTAILEN")]
        public System.DateTime? NgayTaiLen { get; set; }

        // Navigation property và ForeignKey
        [ForeignKey(nameof(MaTour))]
        public Tour? Tour { get; set; }
    }
}
