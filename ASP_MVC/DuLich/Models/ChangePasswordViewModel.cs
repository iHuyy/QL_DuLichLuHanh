using System.ComponentModel.DataAnnotations;

namespace DuLich.Models
{
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Mật khẩu cũ là bắt buộc")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu cũ")]
        public string OldPassword { get; set; }

        [Required(ErrorMessage = "Mật khẩu mới là bắt buộc")]
        [StringLength(100, ErrorMessage = "Mật khẩu mới phải có ít nhất {2} ký tự.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu mới")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Xác nhận mật khẩu mới")]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu mới và mật khẩu xác nhận không khớp.")]
        public string ConfirmPassword { get; set; }
    }
}