using System;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using DuLich.Models;
using DuLich.Models.Data;
using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

#nullable enable

namespace DuLich.Controllers.staff
{
    [Authorize(Roles = "ROLE_STAFF")]
    public class StaffController : BaseController
    {
        private readonly ILogger<StaffController> _logger;
        private readonly IWebHostEnvironment _env;

        public StaffController(ApplicationDbContext context, ILogger<StaffController> logger, IWebHostEnvironment env) : base(context)
        {
            _logger = logger;
            _env = env;
        }

        private static string GetStatusClass(string status)
        {
            return status?.ToLower() switch
            {
                "chờ xử lý" => "warning",
                "đã xác nhận" => "success",
                "đã hủy" => "danger",
                "hoàn thành" => "info",
                _ => "secondary"
            };
        }

        private static string GetTourStatusClass(string status)
        {
            return status?.ToLower() switch
            {
                "hoạt động" => "success",
                "tạm ngưng" => "warning",
                "đã kết thúc" => "info",
                "đã hủy" => "danger",
                "ẩn" => "secondary",
                _ => "secondary"
            };
        }

        private static bool CanCancelBooking(DatTour booking)
        {
            if (booking == null || booking.Tour == null)
                return false;

            if (booking.TrangThaiDat?.ToLower() == "đã hủy" ||
                booking.TrangThaiDat?.ToLower() == "hoàn thành")
                return false;

            return booking.Tour.ThoiGian != null &&
                   booking.Tour.ThoiGian.Value.Date > DateTime.Now.AddDays(3).Date;
        }

        [HttpGet]
        public IActionResult CreateTour()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTour([Bind("TieuDe,NoiDen,NoiKhoiHanh,ThoiGian,SoLuong,GiaNguoiLon,GiaTreEm,MoTa")] Tour tour, List<IFormFile>? TourImages)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Vui lòng kiểm tra lại thông tin nhập vào!";
                return View(tour);
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    tour.TrangThai = "Hoạt động";
                    _context.Tours.Add(tour);
                    await _context.SaveChangesAsync();

                    if (TourImages?.Any() == true)
                    {
                        var imagesPath = Path.Combine(_env.WebRootPath, "images", "tour", tour.MaTour.ToString());
                        Directory.CreateDirectory(imagesPath);

                        foreach (var imageFile in TourImages.Where(f => f?.Length > 0))
                        {
                            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(imageFile.FileName)}";
                            var filePath = Path.Combine(imagesPath, fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await imageFile.CopyToAsync(stream);
                            }

                            var anhTour = new AnhTour
                            {
                                MaTour = tour.MaTour,
                                DuongDanAnh = $"/images/tour/{tour.MaTour}/{fileName}",
                                NgayTaiLen = DateTime.Now
                            };
                            _context.AnhTours.Add(anhTour);
                        }
                    }
                    
                    var tourUrl = Url.Action("TourDetails", "Staff", new { id = tour.MaTour }, Request.Scheme) ?? $"/Staff/TourDetails/{tour.MaTour}";
                    var qrCodeFileName = $"tour_{tour.MaTour}_{DateTime.Now:yyyyMMddHHmmss}.png";
                    var qrCodeDirectory = Path.Combine(_env.WebRootPath, "images", "qrcode");
                    Directory.CreateDirectory(qrCodeDirectory);

                    using (var qrGenerator = new QRCoder.QRCodeGenerator())
                    {
                        var qrCodeData = qrGenerator.CreateQrCode(tourUrl, QRCoder.QRCodeGenerator.ECCLevel.Q);
                        using var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
                        var qrCodeImage = qrCode.GetGraphic(10);
                        var qrCodePath = Path.Combine(qrCodeDirectory, qrCodeFileName);
                        await System.IO.File.WriteAllBytesAsync(qrCodePath, qrCodeImage);
                        tour.QR = $"/images/qrcode/{qrCodeFileName}";
                    }

                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    TempData["Success"] = "Thêm tour mới thành công!";
                    return RedirectToAction(nameof(Tours));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error creating tour. Transaction rolled back.");
                    TempData["Error"] = "Có lỗi xảy ra, không thể tạo tour.";
                    return View(tour);
                }
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditTour(int id)
        {
            var t = await _context.Tours.FindAsync(id);
            if (t == null)
            {
                TempData["Error"] = "Tour không tồn tại hoặc bạn không có quyền sửa tour này.";
                return RedirectToAction(nameof(Tours));
            }
            return View(t);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTour(int id, [Bind("MaTour,TieuDe,NoiDen,NoiKhoiHanh,ThoiGian,SoLuong,GiaNguoiLon,GiaTreEm,MoTa,TrangThai,QR")] Tour tour)
        {
            if (id != tour.MaTour)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tour);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật tour thành công";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Tours.Any(e => e.MaTour == tour.MaTour))
                    {
                        return NotFound();
                    }
                    else
                    {
                        TempData["Error"] = "Tour không tồn tại hoặc bạn không có quyền sửa tour này.";
                    }
                }
                return RedirectToAction(nameof(Tours));
            }
            return View(tour);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTour(int id)
        {
            var t = await _context.Tours.FindAsync(id);
            if (t == null)
            {
                TempData["Error"] = "Tour không tồn tại hoặc bạn không có quyền xóa tour này.";
                return RedirectToAction(nameof(Tours));
            }

            var images = _context.AnhTours.Where(a => a.MaTour == id).ToList();
            if (images.Any())
            {
                _context.AnhTours.RemoveRange(images);
            }

            _context.Tours.Remove(t);

            try
            {
                await _context.SaveChangesAsync();

                foreach (var image in images)
                {
                    if (!string.IsNullOrEmpty(image.DuongDanAnh))
                    {
                        var imagePath = Path.Combine(_env.WebRootPath, image.DuongDanAnh.TrimStart('/'));
                        if (System.IO.File.Exists(imagePath))
                        {
                            System.IO.File.Delete(imagePath);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(t.QR))
                {
                    var qrPath = Path.Combine(_env.WebRootPath, t.QR.TrimStart('/'));
                    if (System.IO.File.Exists(qrPath))
                    {
                        System.IO.File.Delete(qrPath);
                    }
                }

                TempData["Success"] = "Xóa tour thành công";
            }
            catch (DbUpdateException ex)
            {
                var innerException = ex.InnerException;
                if (innerException is Oracle.ManagedDataAccess.Client.OracleException oracleEx && oracleEx.Number == 2292)
                {
                    TempData["Error"] = "Không thể xóa tour. Có thể do đã có khách hàng đặt tour này hoặc có dữ liệu liên quan khác.";
                }
                else
                {
                    TempData["Error"] = "Đã xảy ra lỗi khi xóa tour: " + (innerException?.Message ?? ex.Message);
                }
            }

            return RedirectToAction(nameof(Tours));
        }

        public async Task<IActionResult> Tours()
        {
            var tours = await _context.Tours.AsNoTracking().OrderByDescending(t => t.MaTour).ToListAsync();
            
            var tourViewModels = tours.Select(t => new TourViewModel
            {
                Id = t.MaTour,
                MaTour = t.MaTour.ToString(),
                TenTour = t.TieuDe ?? "Chưa đặt tên",
                DiemDen = t.NoiDen ?? "Chưa xác định",
                NgayKhoiHanh = t.ThoiGian ?? DateTime.Now,
                Gia = t.GiaNguoiLon ?? 0,
                SoLuong = t.SoLuong ?? 0,
                TrangThai = t.TrangThai ?? "Chưa xác định",
                QR = t.QR ?? "",
                StatusClass = GetTourStatusClass(t.TrangThai ?? "")
            }).ToList();

            return View(tourViewModels);
        }

        public IActionResult Index() => View();

        public async Task<IActionResult> Bookings()
        {
            var bookings = await _context.DatTours
                .AsNoTracking()
                .Include(d => d.Tour)
                .Include(d => d.KhachHang)
                .OrderByDescending(d => d.NgayDat)
                .Take(100)
                .Select(d => new BookingViewModel
                {
                    Id = d.MaDatTour,
                    CustomerName = d.KhachHang == null ? "Không xác định" : d.KhachHang.HoTen ?? "Không xác định",
                    TourName = d.Tour == null ? "Không xác định" : d.Tour.TieuDe ?? "Không xác định",
                    BookingDate = d.NgayDat ?? DateTime.Now,
                    Quantity = (d.SoNguoiLon ?? 0) + (d.SoTreEm ?? 0),
                    TotalAmount = d.TongTien ?? 0,
                    Status = d.TrangThaiDat ?? "Chưa xác định",
                    StatusClass = GetStatusClass(d.TrangThaiDat ?? ""),
                    CanCancel = CanCancelBooking(d)
                })
                .ToListAsync();

            return View(bookings);
        }

        public async Task<IActionResult> TourDetails(int id)
        {
             var tour = await _context.Tours
                .Include(t => t.AnhTours)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MaTour == id);

            if (tour == null)
            {
                return NotFound();
            }

            var soLuongDat = await _context.DatTours
                .Where(d => d.MaTour == id && d.TrangThaiDat == "Đã xác nhận")
                .SumAsync(d => (int?)(d.SoNguoiLon ?? 0) + (d.SoTreEm ?? 0)) ?? 0;

            var tourViewModel = new TourViewModel
            {
                Id = tour.MaTour,
                MaTour = tour.MaTour.ToString(),
                TenTour = tour.TieuDe ?? "Chưa đặt tên",
                DiemDen = tour.NoiDen ?? "Chưa xác định",
                NoiKhoiHanh = tour.NoiKhoiHanh ?? "Chưa xác định",
                NgayKhoiHanh = tour.ThoiGian ?? DateTime.Now,
                Gia = tour.GiaNguoiLon ?? 0,
                GiaTreEm = tour.GiaTreEm,
                SoLuong = tour.SoLuong ?? 0,
                SoChoConLai = (tour.SoLuong ?? 0) - soLuongDat,
                TrangThai = tour.TrangThai ?? "Chưa xác định",
                MoTa = tour.MoTa ?? "",
                QR = tour.QR ?? "",
                ChiNhanh = tour.ChiNhanh?.TenChiNhanh ?? "",
                StatusClass = GetTourStatusClass(tour.TrangThai ?? ""),
                AnhTours = tour.AnhTours
            };

            return View(tourViewModel);
        }

        public async Task<IActionResult> Customers()
        {
            var customers = await _context.KhachHangs
                .AsNoTracking()
                .Where(k => k.VaiTro == "KhachHang")
                .Select(k => new CustomerViewModel
                {
                    Id = k.MaKhachHang,
                    MaKH = k.MaKhachHang.ToString(),
                    HoTen = k.HoTen ?? "Không xác định",
                    Email = k.Email ?? "",
                    SoDienThoai = k.SoDienThoai ?? "",
                    NgaySinh = k.NgayTao,
                    DiaChi = k.DiaChi ?? "",
                    SoTourDaDat = _context.DatTours
                        .Where(d => d.MaKhachHang == k.MaKhachHang)
                        .Count()
                })
                .Take(100)
                .ToListAsync();

            return View(customers);
        }

        public IActionResult Reports() => View();

        public async Task<IActionResult> Profile()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login", "Admin");
            }

            var staff = await _context.NhanViens
                .FirstOrDefaultAsync(k => k.ORACLE_USERNAME == username);

            if (staff == null)
            {
                return NotFound();
            }

            return View(staff);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Admin");
        }
    }
}