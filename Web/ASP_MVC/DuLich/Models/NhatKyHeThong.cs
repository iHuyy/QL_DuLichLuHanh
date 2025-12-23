using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DuLich.Models
{
    [Table("NHATKYHETHONG", Schema = "TADMIN")]
    public class NhatKyHeThong
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("MANHATKY")]
        public int MaNhatKy { get; set; }

        [Column("TENBANG")]
        public string? TenBang { get; set; }

        [Column("HANHDONG")]
        public string? HanhDong { get; set; }

        [Column("THOIGIAN")]
        public DateTime ThoiGian { get; set; }

        [Column("NGUOITHUCHIEN")]
        public string? NguoiThucHien { get; set; }
    }
}