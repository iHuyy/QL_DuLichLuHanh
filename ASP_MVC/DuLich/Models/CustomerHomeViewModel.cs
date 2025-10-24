using System.Collections.Generic;

namespace DuLich.Models
{
    public class CustomerHomeViewModel
    {
        public List<TourItem> Tours { get; set; } = new List<TourItem>();
        public List<TourItem> PopularTours { get; set; } = new List<TourItem>();
    }

    public class TourItem
    {
        public int MaTour { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public decimal PriceAdult { get; set; }
        public List<string> Images { get; set; } = new List<string>();
    }
}
