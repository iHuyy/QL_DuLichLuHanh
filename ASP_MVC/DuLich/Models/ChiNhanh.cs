using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DuLich.Models
{
    [Table("CHINHANH", Schema = "TADMIN")]
    public class ChiNhanh
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("MACHINHANH")]
        public int MaChiNhanh { get; set; }

        [Required]
        [Column("TENCHINHANH")]
        public string TenChiNhanh { get; set; } = string.Empty;

        [Column("DIACHI")]
        public string? DiaChi { get; set; }

        [Column("SODIENTHOAI")]
        public string? SoDienThoai { get; set; }

        public ICollection<NhanVien> NhanViens { get; set; } = new List<NhanVien>();
        public ICollection<Tour> Tours { get; set; } = new List<Tour>();
    }
}
