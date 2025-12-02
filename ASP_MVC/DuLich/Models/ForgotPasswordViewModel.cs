using System.ComponentModel.DataAnnotations;

namespace DuLich.Models
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
        public string Username { get; set; } = string.Empty;
    }

    public class VerifyOtpViewModel
    {
        public string Username { get; set; } = string.Empty; // Giữ lại username để biết xác thực cho ai

        [Required(ErrorMessage = "Vui lòng nhập mã xác thực")]
        public string Otp { get; set; } = string.Empty;
    }

    public class ResetPasswordViewModel
    {
        public string Username { get; set; } = string.Empty;

        public string Otp { get; set; } = string.Empty; // Gửi kèm OTP để bảo mật lớp cuối

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [DataType(DataType.Password)]
        [RegularExpression("^(?=.*[A-Z])(?=.*\\d)(?=.*[\\W_]).{8,}$", ErrorMessage = "Mật khẩu phải có tối thiểu 8 ký tự, bao gồm chữ hoa, số và ký tự đặc biệt.")]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}