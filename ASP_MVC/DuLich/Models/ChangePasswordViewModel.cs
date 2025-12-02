using System.ComponentModel.DataAnnotations;

namespace DuLich.Models
{
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Mật khẩu cũ là bắt buộc")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu cũ")]
        public string OldPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu mới là bắt buộc")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu mới")]
        [RegularExpression("^(?=.*[A-Z])(?=.*\\d)(?=.*[\\W_]).{8,}$", ErrorMessage = "Mật khẩu phải có tối thiểu 8 ký tự, bao gồm chữ hoa, số và ký tự đặc biệt.")]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Xác nhận mật khẩu mới")]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu mới và mật khẩu xác nhận không khớp.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}