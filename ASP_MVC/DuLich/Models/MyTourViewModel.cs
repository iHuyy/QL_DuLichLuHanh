using System;
using System.Collections.Generic;

namespace DuLich.Models
{
    public class MyTourViewModel
    {
        public List<MyTourItem> MyTours { get; set; } = new List<MyTourItem>();
        public List<TourItem> PopularTours { get; set; } = new List<TourItem>();
    }

    public class MyTourItem
    {
        public int TourId { get; set; }
        public int BookingId { get; set; }
        public int CheckoutId { get; set; } // Assuming checkoutId is an int, adjust if needed
        public string BookingStatus { get; set; } = string.Empty; // 'b' for pending, 'y' for upcoming, 'f' for finished, 'c' for cancelled
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty; // Consider using DateTime if possible
        public int NumAdults { get; set; }
        public int NumChildren { get; set; }
        public decimal TotalPrice { get; set; }
        public List<string> Images { get; set; } = new List<string>();
        public decimal Rating { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal PriceAdult { get; set; }
        public decimal PriceChild { get; set; }
        public bool IsPaid { get; set; }
    }
}
