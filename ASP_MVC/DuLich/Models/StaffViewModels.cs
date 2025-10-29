using System;

namespace DuLich.Models
{
    public class BookingViewModel
    {
        public int Id { get; set; }
        public string? CustomerName { get; set; }
        public string? TourName { get; set; }
        public DateTime BookingDate { get; set; }
        public int Quantity { get; set; }
        public decimal TotalAmount { get; set; }
        public string? Status { get; set; }
        public string? StatusClass { get; set; }
        public bool CanCancel { get; set; }

        public BookingViewModel()
        {
            CustomerName = "Không xác định";
            TourName = "Không xác định";
            BookingDate = DateTime.Now;
            Status = "Chưa xác định";
            StatusClass = "secondary";
        }
    }

    public class TourViewModel
    {
        public int Id { get; set; }
        public string? MaTour { get; set; }
        public string? TenTour { get; set; }
        public string? DiemDen { get; set; }
        public DateTime NgayKhoiHanh { get; set; }
        public decimal Gia { get; set; }
        public int SoChoConLai { get; set; }
        public int SoLuong { get; set; }
        public string? TrangThai { get; set; }
        public string? StatusClass { get; set; }

        public TourViewModel()
        {
            MaTour = "";
            TenTour = "Chưa đặt tên";
            DiemDen = "Chưa xác định";
            NgayKhoiHanh = DateTime.Now;
            TrangThai = "Chưa xác định";
            StatusClass = "secondary";
        }
    }

    public class CustomerViewModel
    {
        public int Id { get; set; }
        public string? MaKH { get; set; }
        public string? HoTen { get; set; }
        public string? Email { get; set; }
        public string? SoDienThoai { get; set; }
        public DateTime? NgaySinh { get; set; }
        public string? DiaChi { get; set; }
        public int SoTourDaDat { get; set; }

        public CustomerViewModel()
        {
            MaKH = "";
            HoTen = "Không xác định";
            Email = "";
            SoDienThoai = "";
            DiaChi = "";
        }
    }
}