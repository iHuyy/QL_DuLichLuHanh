using System;
using System.ComponentModel.DataAnnotations;

namespace DuLich.Models
{
    public class CreateBookingViewModel
    {
        // Tour Information
        public int TourId { get; set; }
        public string? TourTitle { get; set; }
        public DateTime? StartDate { get; set; }
        public decimal PriceAdult { get; set; }
        public decimal PriceChild { get; set; }
        public int AvailableSlots { get; set; }

        // Contact Information
        [Required(ErrorMessage = "Họ và tên là bắt buộc")]
        public string? FullName { get; set; }

        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Số điện thoại là bắt buộc")]
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }

        // Passenger Details
        [Range(1, int.MaxValue, ErrorMessage = "Phải có ít nhất 1 người lớn")]
        public int NumAdults { get; set; } = 1;

        [Range(0, int.MaxValue, ErrorMessage = "Số trẻ em không hợp lệ")]
        public int NumChildren { get; set; } = 0;

        // Special Request
        public string? SpecialRequest { get; set; }

        // Agreement
        [Range(typeof(bool), "true", "true", ErrorMessage = "Bạn phải đồng ý với điều khoản")]
        public bool AgreeToTerms { get; set; }
    }
}
