using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DuLich.Models
{
    [Table("QR_LOGIN", Schema = "TADMIN")]
    public class QR_Login
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("ID")]
        public int Id { get; set; }

        [Column("SESSIONKEY")]
        public string SessionKey { get; set; }

        [Column("USERID")]
        public int? UserId { get; set; }

        [Column("ISUSED")]
        public int IsUsed { get; set; } // 0 = Pending, 1 = Authenticated

        [Column("CREATEDAT")]
        public DateTime CreatedAt { get; set; }
    }
}
