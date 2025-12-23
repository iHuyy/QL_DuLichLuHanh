using System;

using System.Collections.Generic;

namespace DuLich.Models
{
    public class BookingDetailViewModel
    {
        public int Id { get; set; }
        public DateTime BookingDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusClass { get; set; } = "secondary";
        public int Quantity { get; set; }
        public decimal TotalAmount { get; set; }
        public CustomerInfo Customer { get; set; } = new();
        public TourInfo Tour { get; set; } = new();
        public List<BookingNoteViewModel> BookingNotes { get; set; } = new();
    }

    public class CustomerInfo
    {
        public string? HoTen { get; set; }
        public string? Email { get; set; }
        public string? SoDienThoai { get; set; }
    }

    public class TourInfo
    {
        public int? MaTour { get; set; }
        public string? TenTour { get; set; }
        public string? DiemKhoiHanh { get; set; }
        public string? DiemDen { get; set; }
        public string? MoTa { get; set; }
        public DateTime NgayKhoiHanh { get; set; }
        public DateTime NgayKetThuc { get; set; }
        public decimal Gia { get; set; }
        public string? NgayKhoiHanhStr => NgayKhoiHanh == default ? null : NgayKhoiHanh.ToString("dd/MM/yyyy");
        public string? NgayKetThucStr => NgayKetThuc == default ? null : NgayKetThuc.ToString("dd/MM/yyyy");
    }

    public class BookingNoteViewModel
    {
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusClass { get; set; } = "secondary";
        public string? Note { get; set; }
        public string? StaffName { get; set; }
    }
}
