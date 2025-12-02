using System.ComponentModel.DataAnnotations;

namespace DuLich.Models
{
    public class VerifyRegistrationViewModel
    {
        [Required]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mã OTP.")]
        [Display(Name = "Mã OTP")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải có 6 chữ số.")]
        public string Otp { get; set; } = string.Empty;
    }
}
