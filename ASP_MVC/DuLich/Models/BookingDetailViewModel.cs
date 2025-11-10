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

    public class BookingNoteViewModel
    {
        public DateTime CreatedAt { get; set; }
        public string? Status { get; set; }
        public string? StatusClass { get; set; }
        public string? Note { get; set; }
        public string? StaffName { get; set; }
    }
}