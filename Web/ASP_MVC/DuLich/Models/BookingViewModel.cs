using System;

namespace DuLich.Models
{
    public class BookingViewModel
    {
        public int Id { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string TourName { get; set; } = string.Empty;
        public DateTime BookingDate { get; set; }
        public int Quantity { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusClass { get; set; } = string.Empty;
        public bool CanCancel { get; set; }
    }
}
