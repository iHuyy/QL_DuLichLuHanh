using System;
using System.Collections.Generic;

namespace DuLich.Models
{
    public class BookingDetailViewModel
    {
        public int Id { get; set; }
        public DateTime BookingDate { get; set; }
        public string? Status { get; set; }
        public string? StatusClass { get; set; }
        public int Quantity { get; set; }
        public decimal TotalAmount { get; set; }

        public CustomerDetailViewModel? Customer { get; set; }
        public TourDetailViewModel? Tour { get; set; }
        public List<BookingNoteViewModel>? BookingNotes { get; set; }
    }

    public class CustomerDetailViewModel
    {
        public string? HoTen { get; set; }
        public string? Email { get; set; }
        public string? SoDienThoai { get; set; }
    }

    public class TourDetailViewModel
    {
        public string? TenTour { get; set; }
        public string? MaTour { get; set; }
        public DateTime NgayKhoiHanh { get; set; }
        public DateTime NgayKetThuc { get; set; }
        public string? DiemKhoiHanh { get; set; }
        public string? DiemDen { get; set; }
        public string? MoTa { get; set; }
        public decimal Gia { get; set; }
    }

    public class BookingNoteViewModel
    {
        public DateTime CreatedAt { get; set; }
        public string? Status { get; set; }
        public string? StatusClass { get; set; }
        public string? Note { get; set; }
        public string? StaffName { get; set; }
    }
}