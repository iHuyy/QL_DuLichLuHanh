using System.ComponentModel.DataAnnotations;

namespace DuLich.Models
{
    public class ResendRegistrationOtpRequest
    {
        [Required(ErrorMessage = "Vui lòng cung cấp email.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        public string Email { get; set; } = string.Empty;
    }
}
