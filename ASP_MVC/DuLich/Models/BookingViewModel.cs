using System;

namespace DuLich.Models
{
    public class BookingViewModel
    {
        public int Id { get; set; }
        public string CustomerName { get; set; }
        public string TourName { get; set; }
        public DateTime BookingDate { get; set; }
        public int Quantity { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; }
        public string StatusClass { get; set; }
        public bool CanCancel { get; set; }
    }
}
