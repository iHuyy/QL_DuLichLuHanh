using System.ComponentModel.DataAnnotations;

namespace DuLich.Models
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
        public string Username { get; set; }
    }

    public class VerifyOtpViewModel
    {
        public string Username { get; set; } // Giữ lại username để biết xác thực cho ai

        [Required(ErrorMessage = "Vui lòng nhập mã xác thực")]
        public string Otp { get; set; }
    }

    public class ResetPasswordViewModel
    {
        public string Username { get; set; }

        public string Otp { get; set; } // Gửi kèm OTP để bảo mật lớp cuối

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [StringLength(100, ErrorMessage = "Mật khẩu phải có ít nhất {2} ký tự.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmPassword { get; set; }
    }
}