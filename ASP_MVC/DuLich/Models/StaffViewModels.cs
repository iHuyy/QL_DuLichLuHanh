using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DuLich.Models
{
    public class TourViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Mã Tour")]
        public string MaTour { get; set; } = string.Empty;

        [Display(Name = "Tên Tour")]
        public string TenTour { get; set; } = string.Empty;

        [Display(Name = "Điểm đến")]
        public string DiemDen { get; set; } = string.Empty;

        [Display(Name = "Ngày khởi hành")]
        [DataType(DataType.Date)]
        public DateTime NgayKhoiHanh { get; set; }

        [Display(Name = "Giá")]
        [DataType(DataType.Currency)]
        public decimal Gia { get; set; }

        [Display(Name = "Số chỗ còn lại")]
        public int SoChoConLai { get; set; }

        [Display(Name = "Tổng số chỗ")]
        public int SoLuong { get; set; }

        [Display(Name = "Trạng thái")]
        public string TrangThai { get; set; } = string.Empty;

        [Display(Name = "QR Code")]
        public string QR { get; set; } = string.Empty;

        // Thêm property để xác định class CSS cho trạng thái
        public string StatusClass { get; set; } = string.Empty;

        // Thêm các properties khác nếu cần
        public string? NoiKhoiHanh { get; set; }
        public decimal? GiaTreEm { get; set; }
        public string? MoTa { get; set; }
        public string? ChiNhanh { get; set; }
        public ICollection<AnhTour> AnhTours { get; set; } = new List<AnhTour>();
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

    public class AuditLogViewModel
    {
        [Display(Name = "Loại hành động")]
        public string LoaiHanhDong { get; set; } = string.Empty;

        [Display(Name = "Tên cột")]
        public string? TenCot { get; set; }

        [Display(Name = "Giá trị cũ")]
        public string? GiaTriCu { get; set; }

        [Display(Name = "Giá trị mới")]
        public string? GiaTriMoi { get; set; }

        [Display(Name = "Người thực hiện")]
        public string? NguoiThucHien { get; set; }

        [Display(Name = "Thời gian thực hiện")]
        public DateTime ThoiGianThucHien { get; set; }
    }
}