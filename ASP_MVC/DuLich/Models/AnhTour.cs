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

        [Column("DUONGDANANH")]
        public string? DuongDanAnh { get; set; }

        [Column("MOTA")]
        public string? MoTa { get; set; }

        [Column("NGAYTAILEN")]
        public System.DateTime? NgayTaiLen { get; set; }

        // Sửa thêm: navigation property và ForeignKey để EF dùng cột MATOUR thay vì tạo shadow FK "TourMaTour"
        [ForeignKey(nameof(MaTour))]
        public Tour? Tour { get; set; }
    }
}
