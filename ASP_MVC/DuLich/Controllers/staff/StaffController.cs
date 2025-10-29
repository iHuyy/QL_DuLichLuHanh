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
using System.Data;
using System.Data.Common;

#nullable enable

namespace DuLich.Controllers.staff
{
    [Authorize(Roles = "ROLE_STAFF")]
    public class StaffController : Controller
    {
        private readonly ILogger<StaffController> _logger;
        private readonly ApplicationDbContext _context;

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
                TempData["Error"] = "Vui lòng kiểm tra lại thông tin nhập vào!";
                return View(tour);
            }

            // LẤY CHI NHÁNH TUYỆT ĐỐI TỪ TÀI KHOẢN (KHÔNG DÙNG FALLBACK)
            var identifier = await GetCurrentChiNhanhAsync();
            if (string.IsNullOrEmpty(identifier))
            {
                _logger.LogWarning("Không tìm thấy ChiNhanh cho tài khoản hiện tại. Hủy thao tác tạo tour.");
                TempData["Error"] = "Không thể xác định chi nhánh cho tài khoản. Vui lòng liên hệ quản trị viên.";
                return View(tour);
            }

            var connectionOpenedHere = false;

            try
            {
                // Mở connection 1 lần và sử dụng xuyên suốt cho SET_IDENTIFIER + transaction + các lệnh EF
                if (_context.Database.GetDbConnection().State != ConnectionState.Open)
                {
                    await _context.Database.OpenConnectionAsync();
                    connectionOpenedHere = true;
                }

                var conn = _context.Database.GetDbConnection();
                await SetOracleClientIdentifierOnConnectionAsync(conn, identifier);

                // Bắt đầu transaction (sẽ dùng connection đã mở)
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // 1. Set default values and add the tour to get the ID
                        tour.TrangThai = "Hoạt động";
                        _context.Tours.Add(tour);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation($"Saved initial tour with temporary ID: {tour.MaTour}");

                        // 2. Handle image uploads using the new tour ID
                        if (TourImages?.Any() == true)
                        {
                            var imagesPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "tour", tour.MaTour.ToString());
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

                        // 3. Generate QR code
                        var tourUrl = Url.Action("Detail", "Tour", new { id = tour.MaTour }, Request.Scheme) ?? $"/Tour/Detail/{tour.MaTour}";
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
                            tour.QR = $"/images/qrcode/{qrCodeFileName}";
                        }

                        // 4. Save all changes (images, QR code path) together
                        await _context.SaveChangesAsync();

                        // 5. Commit
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
            finally
            {
                if (connectionOpenedHere)
                {
                    await _context.Database.CloseConnectionAsync();
                }
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
            try
            {
                // Set Oracle CLIENT_IDENTIFIER
                var username = User.Identity?.Name;
                _logger.LogInformation("Tours: Current username = {username}", username);

                // Mở connection và set identifier
                await _context.Database.OpenConnectionAsync();
                var conn = _context.Database.GetDbConnection();
                
                using (var cmd = conn.CreateCommand())
                {
                    // Lấy CHINHANH từ NHANVIEN theo ORACLE_USERNAME
                    cmd.CommandText = @"
                        SELECT CHINHANH 
                        FROM TADMIN.NHANVIEN 
                        WHERE UPPER(ORACLE_USERNAME) = :username";
                    var p = cmd.CreateParameter();
                    p.ParameterName = "username";
                    p.Value = username?.ToUpper();
                    cmd.Parameters.Add(p);

                    var chiNhanh = (await cmd.ExecuteScalarAsync())?.ToString();
                    _logger.LogInformation("Tours: Found ChiNhanh = {chiNhanh}", chiNhanh);

                    if (string.IsNullOrEmpty(chiNhanh))
                    {
                        TempData["Error"] = "Không thể xác định chi nhánh của bạn.";
                        return View(new List<TourViewModel>());
                    }

                    // Set CLIENT_IDENTIFIER
                    using var setCmd = conn.CreateCommand();
                    setCmd.CommandText = "BEGIN DBMS_SESSION.SET_IDENTIFIER(:id); END;";
                    var idParam = setCmd.CreateParameter();
                    idParam.ParameterName = "id";
                    idParam.Value = chiNhanh;
                    setCmd.Parameters.Add(idParam);
                    await setCmd.ExecuteNonQueryAsync();
                }

                // Truy vấn tours sau khi đã set identifier
                var tours = await _context.Tours
                    .AsNoTracking()
                    .Where(t => t.ThoiGian >= DateTime.Today.AddMonths(-1))
                    .OrderByDescending(t => t.ThoiGian)
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

                _logger.LogInformation("Tours: Found {count} tours", tours.Count);

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tours");
                TempData["Error"] = "Có lỗi xảy ra khi tải danh sách tour.";
                return View(new List<TourViewModel>());
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
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

        // Thêm helper lấy chi nhánh hiện tại của user (ưu tiên username, tìm trực tiếp trên DB bằng SQL)
        private async Task<string?> GetCurrentChiNhanhAsync()
        {
            // 1) ưu tiên username từ Identity
            var username = User.Identity?.Name?.Trim();
            _logger.LogInformation("GetCurrentChiNhanhAsync: User.Identity.Name = {username}", username);

            if (!string.IsNullOrEmpty(username))
            {
                try
                {
                    // Mở connection nếu cần
                    var conn = _context.Database.GetDbConnection();
                    if (conn.State != ConnectionState.Open)
                        await conn.OpenAsync();

                    using var cmd = conn.CreateCommand();
                    // Tìm CHINHANH bằng ORACLE_USERNAME hoặc EMAIL (case-insensitive)
                    cmd.CommandText = @"
                        SELECT CHINHANH
                        FROM TADMIN.NHANVIEN
                        WHERE UPPER(ORACLE_USERNAME) = :username
                           OR UPPER(EMAIL) = :username
                        FETCH FIRST 1 ROWS ONLY";
                    var p = cmd.CreateParameter();
                    p.ParameterName = "username";
                    p.Value = username.ToUpperInvariant();
                    cmd.Parameters.Add(p);

                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        var chiNhanh = result.ToString();
                        _logger.LogInformation("GetCurrentChiNhanhAsync: found chi nhánh = {chiNhanh}", chiNhanh);
                        return chiNhanh;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error while trying to get user's ChiNhanh by raw SQL.");
                }
            }

            // 2) nếu không tìm bằng username, thử lấy từ claim "ChiNhanh"
            var claimVal = HttpContext?.User?.FindFirst("ChiNhanh")?.Value;
            if (!string.IsNullOrEmpty(claimVal))
            {
                _logger.LogInformation("GetCurrentChiNhanhAsync: found by claim ChiNhanh");
                return claimVal;
            }

            // 3) không tìm thấy
            _logger.LogInformation("GetCurrentChiNhanhAsync: ChiNhanh not found for user");
            return null;
        }

        // Thêm helper set identifier cho Oracle session
        private async Task SetOracleClientIdentifierAsync(string? identifier = null)
        {
            try
            {
                // nếu không truyền identifier, lấy từ claim hoặc DB
                if (string.IsNullOrEmpty(identifier))
                {
                    identifier = await GetCurrentChiNhanhAsync() ?? "Chi nhánh Hà Nội";
                }

                _logger.LogInformation("Setting Oracle CLIENT_IDENTIFIER = {identifier}", identifier);

                // đảm bảo DbContext mở connection (EF sẽ reuse connection đã mở)
                await _context.Database.OpenConnectionAsync();

                var conn = _context.Database.GetDbConnection();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "BEGIN DBMS_SESSION.SET_IDENTIFIER(:id); END;";
                    var p = cmd.CreateParameter();
                    p.ParameterName = "id";
                    p.Value = identifier;
                    cmd.Parameters.Add(p);

                    await cmd.ExecuteNonQueryAsync();
                }

                // optional: kiểm tra giá trị thực của SYS_CONTEXT để debug
                using (var checkCmd = conn.CreateCommand())
                {
                    checkCmd.CommandText = "SELECT SYS_CONTEXT('USERENV','CLIENT_IDENTIFIER') FROM DUAL";
                    var res = await checkCmd.ExecuteScalarAsync();
                    _logger.LogInformation("Oracle SYS_CONTEXT('CLIENT_IDENTIFIER') = {val}", res ?? "<null>");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể set Oracle CLIENT_IDENTIFIER. Tiếp tục mà không set identifier.");
            }
        }

        // Set CLIENT_IDENTIFIER trên connection đã mở
        private async Task SetOracleClientIdentifierOnConnectionAsync(DbConnection conn, string identifier)
        {
            try
            {
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "BEGIN DBMS_SESSION.SET_IDENTIFIER(:id); END;";
                var p = cmd.CreateParameter();
                p.ParameterName = "id";
                p.Value = identifier;
                cmd.Parameters.Add(p);

                await cmd.ExecuteNonQueryAsync();

                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT SYS_CONTEXT('USERENV','CLIENT_IDENTIFIER') FROM DUAL";
                var res = await checkCmd.ExecuteScalarAsync();
                _logger.LogInformation("Oracle SYS_CONTEXT('CLIENT_IDENTIFIER') = {val}", res ?? "<null>");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể set Oracle CLIENT_IDENTIFIER on the given connection.");
            }
        }
    }
}