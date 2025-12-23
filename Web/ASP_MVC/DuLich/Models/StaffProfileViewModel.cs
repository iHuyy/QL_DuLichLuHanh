using System.ComponentModel.DataAnnotations;

namespace DuLich.Models
{
    public class StaffProfileViewModel
    {
        public int MaNhanVien { get; set; }

        [Required]
        [Display(Name = "Họ tên")]
        public string? HoTen { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Số điện thoại")]
        public string? SoDienThoai { get; set; }

        public string? BranchName { get; set; }

        public ChangePasswordViewModel ChangePassword { get; set; } = new();
    }
}
