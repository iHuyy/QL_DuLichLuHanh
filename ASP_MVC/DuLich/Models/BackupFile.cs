using System.ComponentModel.DataAnnotations.Schema;

namespace DuLich.Models
{
    // This is a DTO (Data Transfer Object) to hold the data from the external table.
    // It does not represent a real table with a primary key in the same way as other entities.
    [NotMapped]
    public class BackupFile
    {
        public string? FILENAME { get; set; }
    }
}
