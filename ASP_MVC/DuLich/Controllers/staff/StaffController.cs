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
using DuLich.Models;
using DuLich.Models.Data;

#nullable enable

namespace DuLich.Controllers.staff
{
    [Authorize(Roles = "ROLE_STAFF")]
    public class StaffController : Controller
    {
        private readonly ILogger<StaffController> _logger;

        public StaffController(ApplicationDbContext context, ILogger<StaffController> logger)
        {
            _context = context;
            _logger = logger;
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

            // Chỉ cho phép hủy trước 3 ngày so với ngày khởi hành
            return booking.Tour.ThoiGian != null &&
                   booking.Tour.ThoiGian.Value.Date > DateTime.Now.AddDays(3).Date;
        }

        // GET: /staff/tour/create
        public IActionResult CreateTour()
        {
            return View();
        }

        // POST: /staff/tour/create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTour([Bind("TieuDe,NoiDen,NoiKhoiHanh,ThoiGian,SoLuong,GiaNguoiLon,GiaTreEm,MoTa")] Tour tour, List<IFormFile>? TourImages)
        {
            if (!ModelState.IsValid)
            {
                foreach (var modelState in ModelState.Values)
                {
                    foreach (var error in modelState.Errors)
                    {
                        _logger.LogError($"Model error: {error.ErrorMessage}");
                    }
                }
                TempData["Error"] = "Vui lòng kiểm tra lại thông tin nhập vào!";
                return View(tour);
            }

            try
            {
                // Thiết lập giá trị mặc định
                tour.TrangThai = "Hoạt động";

                // Thêm tour vào database
                _context.Tours.Add(tour);
                var result = await _context.SaveChangesAsync();

                if (result <= 0)
                {
                    _logger.LogError("Không thể lưu tour vào database");
                    throw new Exception("Không thể lưu tour vào database");
                }

                _logger.LogInformation($"Đã tạo tour mới với ID: {tour.MaTour}");                // Xử lý ảnh nếu có
                var uploadedImages = 0;
                if (TourImages?.Any() == true)
                {
                    var imagesPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "tour", tour.MaTour.ToString());
                    Directory.CreateDirectory(imagesPath);

                    foreach (var imageFile in TourImages.Where(f => f?.Length > 0))
                    {
                        try
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
                            uploadedImages++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Lỗi khi upload ảnh cho tour {tour.MaTour}: {ex.Message}");
                            continue;
                        }
                    }

                    if (uploadedImages > 0)
                    {
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Đã lưu {uploadedImages} ảnh cho tour {tour.MaTour}");
                    }
                }

                try
                {
                    // Tạo QR Code với URL tuyệt đối
                    var tourUrl = Url.Action("Detail", "Tour", new { id = tour.MaTour }, Request.Scheme);
                    if (string.IsNullOrEmpty(tourUrl))
                    {
                        _logger.LogWarning($"Không thể tạo URL cho tour {tour.MaTour}, sử dụng URL tương đối");
                        tourUrl = $"/Tour/Detail/{tour.MaTour}";
                    }

                    // Tạo và lưu QR code
                    var qrCodeFileName = $"tour_{tour.MaTour}_{DateTime.Now:yyyyMMddHHmmss}.png";
                    var qrCodeDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "qrcode");
                    Directory.CreateDirectory(qrCodeDirectory);

                    using (var qrGenerator = new QRCoder.QRCodeGenerator())
                    {
                        var qrCodeData = qrGenerator.CreateQrCode(tourUrl, QRCoder.QRCodeGenerator.ECCLevel.Q);
                        using var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
                        var qrCodeImage = qrCode.GetGraphic(10);
                        var qrCodePath = Path.Combine(qrCodeDirectory, qrCodeFileName);

                        await System.IO.File.WriteAllBytesAsync(qrCodePath, qrCodeImage);
                        _logger.LogInformation($"Đã tạo QR code: {qrCodePath}");

                        // Cập nhật đường dẫn QR trong database
                        tour.QR = $"/images/qrcode/{qrCodeFileName}";
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Lỗi khi tạo QR code cho tour {tour.MaTour}: {ex.Message}");
                    // Không throw exception ở đây vì QR code không phải là thông tin quan trọng
                }                // Hoàn thành và redirect
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm tour mới thành công!";
                return RedirectToAction(nameof(Tours));
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError($"Lỗi database khi tạo tour: {dbEx.Message}");
                ModelState.AddModelError("", "Lỗi khi lưu dữ liệu vào database");
                TempData["Error"] = "Không thể lưu thông tin tour, vui lòng thử lại sau";
                return View(tour);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi tạo tour: {ex.Message}");
                ModelState.AddModelError("", "Có lỗi xảy ra khi tạo tour");
                TempData["Error"] = "Có lỗi xảy ra, vui lòng thử lại sau";
                return View(tour);
            }
        }

        // GET: /staff/tour/details/{id}
        public async Task<IActionResult> TourDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tour = await _context.Tours
                .Include(t => t.AnhTours)
                .FirstOrDefaultAsync(m => m.MaTour == id);

            if (tour == null)
            {
                return NotFound();
            }

            return View(tour);
        }

        private readonly ApplicationDbContext _context;

        // GET: /staff
        public IActionResult Index()
        {
            return View();
        }

        // GET: /staff/bookings
        public async Task<IActionResult> Bookings()
        {
            var bookings = await _context.DatTours
                .AsNoTracking() // Thêm AsNoTracking vì không cần theo dõi thay đổi
                .Include(d => d.Tour)
                .Include(d => d.KhachHang)
                .OrderByDescending(d => d.NgayDat)
                .Take(100) // Giới hạn số lượng bản ghi
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

        // GET: /staff/tours
        public async Task<IActionResult> Tours()
        {
            var tours = await _context.Tours
                .AsNoTracking()
                .Where(t => t.ThoiGian >= DateTime.Today.AddMonths(-1)) // Chỉ lấy tour trong 1 tháng gần đây
                .OrderBy(t => t.ThoiGian)
                .Select(t => new
                {
                    t.MaTour,
                    t.TieuDe,
                    t.NoiDen,
                    t.ThoiGian,
                    t.GiaNguoiLon,
                    t.SoLuong,
                    t.TrangThai,
                    t.QR,
                    SoLuongDat = _context.DatTours
                        .Where(d => d.MaTour == t.MaTour && d.TrangThaiDat == "Đã xác nhận")
                        .Sum(d => (int?)(d.SoNguoiLon ?? 0) + (d.SoTreEm ?? 0)) ?? 0
                })
                .ToListAsync();

            var tourViewModels = tours.Select(t => new TourViewModel
            {
                Id = t.MaTour,
                MaTour = t.MaTour.ToString(),
                TenTour = t.TieuDe ?? "Chưa đặt tên",
                DiemDen = t.NoiDen ?? "Chưa xác định",
                NgayKhoiHanh = t.ThoiGian ?? DateTime.Now,
                Gia = t.GiaNguoiLon ?? 0,
                SoChoConLai = (t.SoLuong ?? 0) - t.SoLuongDat,
                SoLuong = t.SoLuong ?? 0,
                TrangThai = t.TrangThai ?? "Chưa xác định",
                StatusClass = GetTourStatusClass(t.TrangThai ?? "")
            }).ToList();

            return View(tourViewModels);
        }

        // GET: /staff/customers
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
                .Take(100) // Giới hạn số lượng khách hàng hiển thị
                .ToListAsync();

            return View(customers);
        }

        // GET: /staff/reports
        public IActionResult Reports()
        {
            return View();
        }

        // GET: /staff/profile
        public async Task<IActionResult> Profile()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login", "Admin");
            }

            var staff = await _context.KhachHangs
                .FirstOrDefaultAsync(k => k.Email == username);

            if (staff == null)
            {
                return NotFound();
            }

            return View(staff);
        }

        // GET: /staff/api/stats
        [HttpGet]
        [Route("/staff/api/stats")]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                var today = DateTime.Today;
                var stats = new
                {
                    pendingBookings = await _context.DatTours
                        .CountAsync(d => d.TrangThaiDat == "Chờ xử lý"),
                    confirmedBookings = await _context.DatTours
                        .CountAsync(d => d.TrangThaiDat == "Đã xác nhận"),
                    activeTours = await _context.Tours
                        .CountAsync(t => t.ThoiGian >= today),
                    totalCustomers = await _context.KhachHangs
                        .CountAsync()
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi khi lấy thống kê", details = ex.Message });
            }
        }

        // GET: /staff/api/recent-bookings
        [HttpGet]
        [Route("/staff/api/recent-bookings")]
        public async Task<IActionResult> GetRecentBookings()
        {
            try
            {
                var bookings = await _context.DatTours
                    .Include(d => d.Tour)
                    .Include(d => d.KhachHang)
                    .OrderByDescending(d => d.NgayDat)
                    .Take(10)
                    .Select(d => new
                    {
                        maDatTour = d.MaDatTour,
                        CustomerName = d.KhachHang == null ? "Không xác định" : d.KhachHang.HoTen ?? "Không xác định",
                        TourName = d.Tour == null ? "Không xác định" : d.Tour.TieuDe ?? "Không xác định",
                        ngayDat = d.NgayDat,
                        trangThai = d.TrangThaiDat
                    })
                    .ToListAsync();

                return Ok(new { data = bookings });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi khi lấy danh sách đặt tour", details = ex.Message });
            }
        }

        // GET: /staff/api/upcoming-tours
        [HttpGet]
        [Route("/staff/api/upcoming-tours")]
        public async Task<IActionResult> GetUpcomingTours()
        {
            try
            {
                var today = DateTime.Today;
                var tours = await _context.Tours
                    .Where(t => t.ThoiGian >= today)
                    .OrderBy(t => t.ThoiGian)
                    .Take(10)
                    .Select(t => new
                    {
                        maTour = t.MaTour,
                        tenTour = t.TieuDe,
                        ngayKhoiHanh = t.ThoiGian,
                        soKhach = _context.DatTours
                            .Where(d => d.MaTour == t.MaTour && d.TrangThaiDat == "Đã xác nhận")
                            .Sum(d => d.SoNguoiLon + d.SoTreEm),
                        trangThai = t.TrangThai
                    })
                    .ToListAsync();

                return Ok(new { data = tours });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi khi lấy danh sách tour", details = ex.Message });
            }
        }

        // GET: /staff/api/notifications
        [HttpGet]
        [Route("/staff/api/notifications")]
        public async Task<IActionResult> GetNotifications()
        {
            try
            {
                var bookings = await _context.DatTours
                    .Include(d => d.Tour)
                    .Include(d => d.KhachHang)
                    .Where(d => d.TrangThaiDat == "Chờ xử lý")
                    .OrderByDescending(d => d.NgayDat)
                    .Take(5)
                    .Select(d => new
                    {
                        d.NgayDat,
                        CustomerName = d.KhachHang == null ? null : d.KhachHang.HoTen,
                        TourName = d.Tour == null ? null : d.Tour.TieuDe
                    })
                    .ToListAsync();

                var notifications = bookings.Select(b => new
                {
                    type = "warning",
                    message = $"Đơn đặt tour mới từ {b.CustomerName ?? "Khách hàng"} cho tour {b.TourName ?? "Không xác định"}",
                    time = b.NgayDat
                }).ToList();

                return Ok(notifications);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi khi lấy thông báo", details = ex.Message });
            }
        }
    }
}