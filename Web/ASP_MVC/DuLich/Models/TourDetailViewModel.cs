using System;

namespace DuLich.Models
{
    public class TourDetailViewModel
    {
        public int MaTour { get; set; }
        public string TenTour { get; set; } = string.Empty;
        public string? MoTa { get; set; }
        public string DiemKhoiHanh { get; set; } = string.Empty;
        public string DiemDen { get; set; } = string.Empty;
        public DateTime NgayKhoiHanh { get; set; }
        public DateTime NgayKetThuc { get; set; }
        public decimal Gia { get; set; }
        public int SoLuong { get; set; }
    }
}