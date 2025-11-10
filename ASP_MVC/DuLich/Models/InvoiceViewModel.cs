using System;

namespace DuLich.Models
{
    public class InvoiceViewModel
    {
        public int MaHoaDon { get; set; }
        public DateTime? NgayXuat { get; set; }
        public decimal? SoTien { get; set; }
        public string? TrangThai { get; set; }
        public bool IsSignatureValid { get; set; }

        // Thông tin tour
        public string? TenTour { get; set; }
        public DateTime? NgayKhoiHanh { get; set; }
        public int? SoNguoiLon { get; set; }
        public int? SoTreEm { get; set; }

        // Thông tin khách hàng
        public string? TenKhachHang { get; set; }
        public string? Email { get; set; }
        public string? SoDienThoai { get; set; }
        public string? DiaChi { get; set; }
    }
}