using DuLich.Models;
using DuLich.Services;
using DuLich.Models.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Oracle.ManagedDataAccess.Client;
using iText.Kernel.Pdf;
using iText.Kernel.Geom;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Font;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Layout.Borders;
using iText.IO.Font;
using Microsoft.Extensions.Caching.Memory;
using System.Data;
using System.Data.Common;
namespace DuLich.Controllers
{
    public class CustomerController : BaseController
    {
        private readonly OracleAuthService _authService;
        private readonly RSAService _rsaService;
        private readonly ApplicationDbContext _dbContext;
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache;
        private readonly EmailService _emailService;
        public CustomerController(OracleAuthService authService, ApplicationDbContext context, RSAService rsaService, IWebHostEnvironment env, IMemoryCache cache, EmailService emailService) : base(context)
        {
            _authService = authService;
            _rsaService = rsaService;
            _dbContext = context;
            _env = env;
            _cache = cache;
            _emailService = emailService;
        }
        [HttpGet]
        public IActionResult Login()
        {
            if (TempData["SuccessMessage"] != null)
            {
                ViewBag.SuccessMessage = TempData["SuccessMessage"];
            }
            return View();
        }

        [HttpGet]
        public IActionResult About()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Login(LoginModel model)
        {
            if (!ModelState.IsValid)
                return View(model);
            var (success, role, errorMessage) = await _authService.ValidateLoginAsync(model.Username, model.Password);
            if (!success && errorMessage == "PASSWORD_EXPIRED")
            {
                return RedirectToAction("ForceChangePassword", new { username = model.Username });
            }
            if (!success && !string.IsNullOrWhiteSpace(errorMessage))
            {
                var message = errorMessage;
                if (errorMessage.IndexOf("khóa", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    message = "Tài khoản của bạn đã bị khóa! Vui lòng liên hệ admin để được hỗ trợ.";
                }
                ModelState.AddModelError(string.Empty, message);
                return View(model);
            }
            Console.WriteLine($"ValidateLoginAsync returned success={success}, role={role} for user={model.Username}");
            if (success && (role == "ROLE_CUSTOMER" || role == "ROLE_ADMIN" || role == "ROLE_STAFF"))
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, model.Username),
                    new Claim(ClaimTypes.Role, role)
                };
                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(15)
                };
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);
                Console.WriteLine("SignInAsync completed for user: " + model.Username);
                try
                {
                    var customer = await _context.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME.ToUpper() == model.Username.ToUpper());
                    var sessionId = GenerateSessionId();
                    if (customer != null)
                    {
                        var userSession = new UserSession
                        {
                            SessionId = sessionId,
                            UserId = customer.MaKhachHang,
                            UserType = role,
                            DeviceType = "WEB",
                            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                            DeviceInfo = Request.Headers["User-Agent"].ToString(),
                            IsActive = "Y",
                            LoginTime = DateTime.UtcNow,
                            LastActivity = DateTime.UtcNow
                        };
                        var prev = _dbContext.UserSessions
                            .Where(s => s.UserId == customer.MaKhachHang && s.DeviceType == userSession.DeviceType)
                            .ToList();
                        if (prev.Any())
                        {
                            _dbContext.UserSessions.RemoveRange(prev);
                        }
                        _dbContext.UserSessions.Add(userSession);
                        await _dbContext.SaveChangesAsync();
                        var cookieOptions = new Microsoft.AspNetCore.Http.CookieOptions
                        {
                            HttpOnly = true,
                            Secure = Request.IsHttps,
                            SameSite = Request.IsHttps ? Microsoft.AspNetCore.Http.SameSiteMode.None : Microsoft.AspNetCore.Http.SameSiteMode.Lax
                        };
                        Response.Cookies.Append("USER_SESSION_ID", sessionId, cookieOptions);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to create user session: " + ex.Message);
                }
                if (role == "ROLE_ADMIN") return RedirectToAction("Dashboard", "Admin");
                if (role == "ROLE_STAFF") return RedirectToAction("Index", "Staff");
                return RedirectToAction("Index", "Customer");
            }
            ModelState.AddModelError(string.Empty, errorMessage ?? "Tên đăng nhập hoặc mật khẩu không chính xác.");
            return View(model);
        }
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> Index(string? destination, string? start_date, string? end_date, string? keyword)
        {
            var q = _context.Tours.AsNoTracking().Where(t => t.TrangThai == "Hoạt động");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var model = new CustomerHomeViewModel();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var keywordUpper = keyword.Trim().ToUpper();
                var hasNumericId = int.TryParse(keyword.Trim(), out var tourId);
                q = q.Where(t =>
                    (t.TieuDe != null && t.TieuDe!.ToUpper().Contains(keywordUpper))
                    || (t.MoTa != null && t.MoTa!.ToUpper().Contains(keywordUpper))
                    || (t.NoiDen != null && t.NoiDen!.ToUpper().Contains(keywordUpper))
                    || (t.NoiKhoiHanh != null && t.NoiKhoiHanh!.ToUpper().Contains(keywordUpper))
                    || (t.ThanhPho != null && t.ThanhPho!.ToUpper().Contains(keywordUpper))
                    || (t.QR != null && t.QR!.ToUpper().Contains(keywordUpper))
                    || (hasNumericId && t.MaTour == tourId)
                );
            }
            if (!string.IsNullOrWhiteSpace(destination))
            {
                var destUpper = destination.Trim().ToUpper();
                q = q.Where(t => (t.NoiDen != null && t.NoiDen!.ToUpper().Contains(destUpper))
                                 || (t.NoiKhoiHanh != null && t.NoiKhoiHanh!.ToUpper().Contains(destUpper))
                                 || (t.ThanhPho != null && t.ThanhPho!.ToUpper().Contains(destUpper)));
            }
            DateTime fromDate, toDate;
            var hasFrom = DateTime.TryParse(start_date, out fromDate);
            var hasTo = DateTime.TryParse(end_date, out toDate);
            if (hasFrom && hasTo)
            {
                if (fromDate > toDate) (fromDate, toDate) = (toDate, fromDate);
                q = q.Where(t => t.ThoiGian.HasValue && t.ThoiGian.Value.Date >= fromDate.Date && t.ThoiGian.Value.Date <= toDate.Date);
            }
            else if (hasFrom)
            {
                q = q.Where(t => t.ThoiGian.HasValue && t.ThoiGian.Value.Date >= fromDate.Date);
            }
            else if (hasTo)
            {
                q = q.Where(t => t.ThoiGian.HasValue && t.ThoiGian.Value.Date <= toDate.Date);
            }
            try
            {
                var toursTask = q.OrderBy(t => t.MaTour).Take(20).ToListAsync(cts.Token);
                var finished = await Task.WhenAny(toursTask, Task.Delay(TimeSpan.FromSeconds(5), cts.Token));
                if (finished != toursTask)
                {
                    // Hủy bỏ nếu quá 5s để tránh treo trang
                    cts.Cancel();
                    ViewBag.LoadError = "Hệ thống đang chậm hoặc mất kết nối dữ liệu. Vui lòng thử lại sau ít phút.";
                    return View("Home", model);
                }
                var tours = await toursTask; // đã hoàn thành trong 5s
                var tourIds = tours.Select(t => t.MaTour).ToList();
                var imagesByTourId = (await _context.AnhTours
                    .Where(a => tourIds.Contains(a.MaTour))
                    .Select(a => new { a.MaTour, a.MaAnh })
                    .ToListAsync(cts.Token))
                    .ToLookup(a => a.MaTour, a => $"/api/image/{a.MaAnh}");
                foreach (var t in tours)
                {
                    model.Tours.Add(new TourItem
                    {
                        MaTour = t.MaTour,
                        Title = t.TieuDe ?? string.Empty,
                        Destination = t.NoiDen ?? t.NoiKhoiHanh ?? t.ThanhPho ?? string.Empty,
                        Time = t.ThoiGian?.ToString("yyyy-MM-dd") ?? string.Empty,
                        PriceAdult = t.GiaNguoiLon ?? 0,
                        Images = imagesByTourId.Contains(t.MaTour) ? imagesByTourId[t.MaTour].ToList() : new List<string>(),
                        QR = t.QR ?? string.Empty
                    });
                }
                model.PopularTours = model.Tours.Take(4).ToList();
                return View("Home", model);
            }
            catch (OperationCanceledException ex)
            {
                Console.WriteLine($"[Customer/Index] Timeout khi tải tour: {ex.Message}");
                ViewBag.LoadError = "Hệ thống đang chậm hoặc mất kết nối dữ liệu. Vui lòng thử lại sau ít phút.";
                return View("Home", model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Customer/Index] Lỗi tải tour: {ex}");
                ViewBag.LoadError = "Không thể tải danh sách tour lúc này. Vui lòng thử lại sau.";
                return View("Home", model);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterModel model)
        {
            if (!ModelState.IsValid)
                return View(model);
            // Step 1: Call the service to validate user and send OTP
            var (success, message) = await _authService.PrepareRegistrationAndSendOtpAsync(model);
            if (success)
            {
                // Redirect to the OTP verification page, passing the email along.
                return RedirectToAction("VerifyRegistrationOtp", new { email = model.Email });
            }
            // If preparation failed (e.g., user exists), show the error.
            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }
        [HttpGet]
        public IActionResult VerifyRegistrationOtp(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Register");
            }
            var model = new VerifyRegistrationViewModel { Email = email };
            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendOtp([FromBody] ResendRegistrationOtpRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Email))
            {
                return BadRequest(new { success = false, message = "Email không hợp lệ. Vui lòng quay lại trang đăng ký rồi thử lại." });
            }
            var (success, message) = await _authService.ResendOtpAsync(request.Email);
            return Json(new { success, message });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyRegistrationOtp(VerifyRegistrationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            // Step 2: Call the service to verify OTP and create the user.
            var (success, message) = await _authService.VerifyAndCompleteRegistrationAsync(model.Email, model.Otp);
            if (success)
            {
                TempData["SuccessMessage"] = message;
                return RedirectToAction("Login");
            }
            // If verification failed, show the error on the OTP page.
            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var username = User?.Identity?.Name;
                if (!string.IsNullOrEmpty(username))
                {
                    int userId;
                    var customer = await _dbContext.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME != null && k.ORACLE_USERNAME.ToUpper() == username.ToUpper());
                    if (customer != null)
                    {
                        userId = customer.MaKhachHang;
                    }
                    else
                    {
                        var staff = await _dbContext.NhanViens.FirstOrDefaultAsync(n => n.ORACLE_USERNAME != null && n.ORACLE_USERNAME.ToUpper() == username.ToUpper());
                        if (staff != null)
                        {
                            userId = staff.MaNhanVien;
                        }
                        else
                        {
                            userId = -1;
                        }
                    }
                    if (userId != -1)
                    {
                        var sessions = _dbContext.UserSessions.Where(s => s.UserId == userId).ToList();
                        if (sessions.Any())
                        {
                            _dbContext.UserSessions.RemoveRange(sessions);
                            await _dbContext.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        var sessionId = Request.Cookies["USER_SESSION_ID"];
                        if (!string.IsNullOrEmpty(sessionId))
                        {
                            var sess = await _dbContext.UserSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
                            if (sess != null)
                            {
                                _dbContext.UserSessions.Remove(sess);
                                await _dbContext.SaveChangesAsync();
                            }
                        }
                    }
                }
                else
                {
                    var sessionId = Request.Cookies["USER_SESSION_ID"];
                    if (!string.IsNullOrEmpty(sessionId))
                    {
                        var sess = await _dbContext.UserSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
                        if (sess != null)
                        {
                            _dbContext.UserSessions.Remove(sess);
                            await _dbContext.SaveChangesAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to deactivate session on logout: " + ex.Message);
            }
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            // remove cookie
            Response.Cookies.Delete("USER_SESSION_ID");
            return RedirectToAction("Login", "Customer");
        }
        private static string GenerateSessionId()
        {
            return Guid.NewGuid().ToString("N");
        }
        [HttpGet]
        [Authorize(Roles = "ROLE_CUSTOMER,ROLE_ADMIN,ROLE_STAFF")]
        public async Task<IActionResult> MyTour()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                // Handle the case where the username is not available.
                // This might mean redirecting to login or returning an error.
                return RedirectToAction("Login");
            }
            var customer = await _context.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME != null && k.ORACLE_USERNAME.ToUpper() == username.ToUpper());
            if (customer == null)
            {
                return RedirectToAction("Login"); // Redirect to login if customer not found
            }
            await UpdateDepartedBookingsToCompletedAsync();
            var model = new MyTourViewModel();
            // Fetch booked tours for the current customer
            var bookedTours = await _context.DatTours
                .Where(dt => dt.MaKhachHang == customer.MaKhachHang)
                .Include(dt => dt.Tour) // Include Tour details
                .Include(dt => dt.HoaDon) // Include HoaDon details
                .OrderByDescending(dt => dt.MaDatTour)
                .ToListAsync();
            foreach (var booking in bookedTours)
            {
                var tour = booking.Tour;
                if (tour == null) continue;
                var imageIds = await _context.AnhTours
                    .Where(a => a.MaTour == tour.MaTour)
                    .OrderBy(a => a.MaAnh)
                    .Select(a => a.MaAnh)
                    .ToListAsync();
                var status = booking.TrangThaiDat ?? string.Empty;
                string bookingStatusChar = "b"; // Default to pending
                if (status == "Hoàn thành")
                {
                    bookingStatusChar = "f"; // Finished
                }
                else if (status == "Đã xác nhận" && tour.ThoiGian > DateTime.Now)
                {
                    bookingStatusChar = "y"; // Upcoming
                }
                else if (status == "Đã xác nhận" && tour.ThoiGian <= DateTime.Now)
                {
                    bookingStatusChar = "f"; // Finished
                }
                else if (status == "Đã hủy")
                {
                    bookingStatusChar = "c"; // Cancelled
                }
                model.MyTours.Add(new MyTourItem
                {
                    TourId = tour.MaTour,
                    BookingId = booking.MaDatTour,
                    CheckoutId = booking.HoaDon?.MaHoaDon ?? 0, // Assign MaHoaDon as CheckoutId
                    BookingStatus = bookingStatusChar,
                    Title = tour.TieuDe ?? string.Empty,
                    Description = tour.MoTa ?? string.Empty,
                    Destination = tour.NoiDen ?? tour.NoiKhoiHanh ?? tour.ThanhPho ?? string.Empty,
                    Time = tour.ThoiGian?.ToString("yyyy-MM-dd") ?? string.Empty,
                    NumAdults = booking.SoNguoiLon ?? 0,
                    NumChildren = booking.SoTreEm ?? 0,
                    TotalPrice = booking.TongTien ?? 0,
                    Images = imageIds.Select(id => $"/api/image/{id}").ToList(),
                    IsPaid = booking.HoaDon?.TrangThai == "Đã thanh toán"
                });
            }
            // Fetch popular tours (similar logic to Index action)
            var popularToursQuery = _context.Tours.Where(t => t.TrangThai == "Hoạt động");
            var popularTours = await popularToursQuery.OrderBy(t => t.MaTour).Take(4).ToListAsync();
            foreach (var t in popularTours)
            {
                var imageIds = await _context.AnhTours
                    .Where(a => a.MaTour == t.MaTour)
                    .OrderBy(a => a.MaAnh)
                    .Select(a => a.MaAnh)
                    .ToListAsync();
                model.PopularTours.Add(new TourItem
                {
                    MaTour = t.MaTour,
                    Title = t.TieuDe ?? string.Empty,
                    Destination = t.NoiDen ?? t.NoiKhoiHanh ?? t.ThanhPho ?? string.Empty,
                    Time = t.ThoiGian?.ToString("yyyy-MM-dd") ?? string.Empty,
                    PriceAdult = t.GiaNguoiLon ?? 0,
                    Images = imageIds.Select(id => $"/api/image/{id}").ToList()
                });
            }
            return View(model);
        }
        [HttpGet]
        public async Task<IActionResult> TourDetail(int id)
        {
            var tour = await _context.Tours.FindAsync(id);
            if (tour == null) return NotFound();

            // 1. Tính tổng số chỗ đã đặt (dựa vào bảng DatTour) - Hàm này lấy từ BaseController
            int totalBooked = await GetReservedSeatCountAsync(id);

            // 2. Tính số chỗ còn lại để hiển thị
            // Lưu ý: tour.SoLuong bây giờ được coi là TỔNG SỨC CHỨA CỐ ĐỊNH
            int totalCapacity = tour.SoLuong ?? 0;
            int remainingSlots = Math.Max(0, totalCapacity - totalBooked);

            var model = new TourDetailViewModel
            {
                MaTour = tour.MaTour,
                TenTour = tour.TieuDe ?? "Chưa có tên",
                MoTa = tour.MoTa,
                DiemKhoiHanh = tour.NoiKhoiHanh ?? "Chưa xác định",
                DiemDen = tour.NoiDen ?? tour.ThanhPho ?? "Chưa xác định",
                NgayKhoiHanh = tour.ThoiGian ?? DateTime.Now,
                NgayKetThuc = tour.ThoiGian?.AddDays(3) ?? DateTime.Now.AddDays(3),
                Gia = tour.GiaNguoiLon ?? 0,
                
                // QUAN TRỌNG: Truyền số chỗ còn lại đã tính toán ra View
                SoLuong = remainingSlots 
            };

            ViewBag.ImageIds = await _context.AnhTours
                .Where(a => a.MaTour == id)
                .OrderBy(a => a.MaAnh)
                .Select(a => a.MaAnh)
                .ToListAsync();

            // Load tour liên quan (giữ nguyên logic cũ)
            ViewBag.RelatedTours = await _context.Tours
                .Where(t => t.MaTour != id && (t.NoiDen == model.DiemDen || t.ThanhPho == model.DiemDen))
                .OrderBy(t => t.MaTour)
                .Take(3)
                .Select(t => new TourDetailViewModel
                {
                    MaTour = t.MaTour,
                    TenTour = t.TieuDe ?? "Chưa xác định",
                    Gia = t.GiaNguoiLon ?? 0
                })
                .ToListAsync();

            return View(model);
        }
        [HttpGet]
        [Authorize(Roles = "ROLE_CUSTOMER,ROLE_ADMIN,ROLE_STAFF")]
        public async Task<IActionResult> Booking(int id)
        {
            var tour = await _context.Tours.FindAsync(id);
            if (tour == null) return NotFound();

            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login");
            
            var customer = await _context.KhachHangs
                .FirstOrDefaultAsync(k => k.ORACLE_USERNAME == username.ToUpper());

            // 1. Tính toán lại số liệu thực tế
            int totalBooked = await GetReservedSeatCountAsync(id);
            int totalCapacity = tour.SoLuong ?? 0;
            int remainingSlots = Math.Max(0, totalCapacity - totalBooked);

            var model = new CreateBookingViewModel
            {
                TourId = tour.MaTour,
                TourTitle = tour.TieuDe,
                StartDate = tour.ThoiGian,
                PriceAdult = tour.GiaNguoiLon ?? 0,
                PriceChild = tour.GiaTreEm ?? 0,
                
                // Hiển thị đúng số chỗ còn lại và tổng sức chứa
                AvailableSlots = remainingSlots, 
                TotalSlots = totalCapacity,
                
                FullName = customer?.HoTen,
                Email = customer?.Email,
                PhoneNumber = customer?.SoDienThoai,
                Address = customer?.DiaChi
            };
            return View(model);
        }
        [HttpPost]
        [Authorize(Roles = "ROLE_CUSTOMER,ROLE_ADMIN,ROLE_STAFF")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Booking([FromForm] CreateBookingViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var tour = await _context.Tours.FindAsync(model.TourId);
            if (tour == null) return NotFound();

            var username = User.Identity?.Name;
            var customer = await _context.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME == username.ToUpper());
            if (customer == null) return Unauthorized();

            // --- BƯỚC KIỂM TRA QUAN TRỌNG ---
            // 1. Tính lại số chỗ đã đặt hiện tại (Concurrency check)
            int currentBooked = await GetReservedSeatCountAsync(model.TourId);
            int totalCapacity = tour.SoLuong ?? 0;
            int remaining = totalCapacity - currentBooked;
            int requestQty = model.NumAdults + model.NumChildren;

            // 2. Kiểm tra xem còn đủ chỗ không
            if (requestQty > remaining)
            {
                ModelState.AddModelError(string.Empty, $"Rất tiếc, tour chỉ còn {remaining} chỗ trống. Bạn đang đặt {requestQty} chỗ.");
                
                // Cập nhật lại số hiển thị cho view để khách biết
                model.AvailableSlots = remaining;
                model.TotalSlots = totalCapacity;
                return View(model);
            }
            // ----------------------------------

            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    // [THAY ĐỔI LỚN]: Đã XÓA bỏ đoạn code trừ SOLUONG trong bảng TOUR
                    // Chỉ thực hiện thêm mới Booking và Hóa đơn

                    // 1. Tạo Booking
                    var booking = new DatTour
                    {
                        MaTour = model.TourId,
                        MaKhachHang = customer.MaKhachHang,
                        NgayDat = DateTime.Now,
                        SoNguoiLon = model.NumAdults,
                        SoTreEm = model.NumChildren,
                        TongTien = (model.NumAdults * (tour.GiaNguoiLon ?? 0)) + (model.NumChildren * (tour.GiaTreEm ?? 0)),
                        TrangThaiDat = "Chờ xác nhận",
                        YeuCauDacBiet = model.SpecialRequest
                    };
                    
                    _context.DatTours.Add(booking);
                    await _context.SaveChangesAsync(); // Lưu để lấy MaDatTour

                    // 2. Tạo Hóa đơn
                    var hoaDon = new HoaDon
                    {
                        MaDatTour = booking.MaDatTour,
                        NgayXuat = DateTime.Now,
                        SoTien = booking.TongTien,
                        TrangThai = "Chưa thanh toán"
                    };
                    _context.HoaDons.Add(hoaDon);
                    await _context.SaveChangesAsync(); // Lưu để lấy MaHoaDon

                    // 3. Ký số hóa đơn
                    var payload = InvoiceSignatureHelper.CreatePayload(booking, hoaDon);
                    hoaDon.ChuKySo = _rsaService.Sign(payload);
                    hoaDon.Payload = payload; // Lưu payload gốc
                    
                    _context.HoaDons.Update(hoaDon);
                    await _context.SaveChangesAsync();

                    transaction.Commit();
                    return RedirectToAction("Payment", new { bookingId = booking.MaDatTour });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    ModelState.AddModelError("", "Lỗi khi đặt tour: " + ex.Message);
                    return View(model);
                }
            }
        }
        [HttpGet]
        [Authorize(Roles = "ROLE_CUSTOMER,ROLE_ADMIN,ROLE_STAFF")]
        public async Task<IActionResult> Profile()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                // Handle the case where the username is not available.
                // This might mean redirecting to login or returning an error.
                return RedirectToAction("Login");
            }
            var customer = await _context.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME != null && k.ORACLE_USERNAME.ToUpper() == username.ToUpper());
            if (customer == null)
            {
                return NotFound(); // Or redirect to login
            }
            var model = new CustomerProfileViewModel
            {
                MaKhachHang = customer.MaKhachHang,
                HoTen = customer.HoTen,
                Email = customer.Email,
                SoDienThoai = customer.SoDienThoai,
                DiaChi = customer.DiaChi
            };
            return View(model);
        }
        [HttpPost]
        [Authorize(Roles = "ROLE_CUSTOMER,ROLE_ADMIN,ROLE_STAFF")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(CustomerProfileViewModel model)
        {
            // 1. Validate dữ liệu đầu vào
            if (!ModelState.IsValid)
            {
                var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Vui lòng kiểm tra dữ liệu.";
                return Json(new { success = false, message = firstError });
            }
            try
            {
                var username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username)) return Json(new { success = false, message = "Phiên đăng nhập hết hạn." });
                // 2. Mở kết nối Database (Dùng kết nối cấp thấp ADO.NET)
                var conn = _dbContext.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open) await conn.OpenAsync();
                // 3. Tìm MaKhachHang dựa trên Username
                // (Dùng SQL thuần để tránh mọi tác động của EF Core)
                int maKhachHang = 0;
                string oldEmail = "";
                using (var cmdGet = conn.CreateCommand())
                {
                    cmdGet.CommandText = "SELECT MAKHACHHANG, EMAIL FROM TADMIN.KHACHHANG WHERE UPPER(ORACLE_USERNAME) = UPPER(:u)";
                    var pUser = cmdGet.CreateParameter();
                    pUser.ParameterName = "u";
                    pUser.Value = username;
                    cmdGet.Parameters.Add(pUser);
                    using (var reader = await cmdGet.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            maKhachHang = Convert.ToInt32(reader["MAKHACHHANG"]);
                            oldEmail = reader["EMAIL"]?.ToString() ?? "";
                        }
                        else
                        {
                            return Json(new { success = false, message = "Không tìm thấy thông tin người dùng." });
                        }
                    }
                }
                // 4. Kiểm tra trùng Email (nếu có thay đổi)
                var newEmail = model.Email?.Trim();
                if (!string.Equals(oldEmail, newEmail, StringComparison.OrdinalIgnoreCase))
                {
                    using (var cmdCheck = conn.CreateCommand())
                    {
                        cmdCheck.CommandText = "SELECT COUNT(*) FROM TADMIN.KHACHHANG WHERE EMAIL = :em AND MAKHACHHANG <> :id";
                        var pEmail = cmdCheck.CreateParameter(); pEmail.ParameterName = "em"; pEmail.Value = newEmail; cmdCheck.Parameters.Add(pEmail);
                        var pId = cmdCheck.CreateParameter(); pId.ParameterName = "id"; pId.Value = maKhachHang; cmdCheck.Parameters.Add(pId);
                        var count = Convert.ToInt32(await cmdCheck.ExecuteScalarAsync());
                        if (count > 0) return Json(new { success = false, message = "Email này đã được sử dụng." });
                    }
                }
                // 5. THỰC HIỆN UPDATE (QUAN TRỌNG NHẤT)
                // Sử dụng SQL thuần túy, tham số hóa đầy đủ
                using (var cmdUpdate = conn.CreateCommand())
                {
                    cmdUpdate.CommandText = @"
                UPDATE TADMIN.KHACHHANG 
                SET HOTEN = :ht, 
                    EMAIL = :em, 
                    SODIENTHOAI = :sdt, 
                    DIACHI = :dc 
                WHERE MAKHACHHANG = :mkh";
                    // Tạo tham số an toàn
                    var pHoTen = cmdUpdate.CreateParameter(); pHoTen.ParameterName = "ht"; pHoTen.Value = (object?)model.HoTen ?? DBNull.Value; cmdUpdate.Parameters.Add(pHoTen);
                    var pMail = cmdUpdate.CreateParameter(); pMail.ParameterName = "em"; pMail.Value = (object?)newEmail ?? DBNull.Value; cmdUpdate.Parameters.Add(pMail);
                    var pPhone = cmdUpdate.CreateParameter(); pPhone.ParameterName = "sdt"; pPhone.Value = (object?)model.SoDienThoai ?? DBNull.Value; cmdUpdate.Parameters.Add(pPhone);
                    var pAddr = cmdUpdate.CreateParameter(); pAddr.ParameterName = "dc"; pAddr.Value = (object?)model.DiaChi ?? DBNull.Value; cmdUpdate.Parameters.Add(pAddr);
                    var pId = cmdUpdate.CreateParameter(); pId.ParameterName = "mkh"; pId.Value = maKhachHang; cmdUpdate.Parameters.Add(pId);
                    // Thực thi
                    await cmdUpdate.ExecuteNonQueryAsync();
                }
                return Json(new { success = true, message = "Cập nhật thông tin thành công!" });
            }
            catch (Exception ex)
            {
                // Log lỗi chi tiết
                Console.WriteLine($"Update Error: {ex.Message}");
                return Json(new { success = false, message = "Lỗi cập nhật: " + ex.Message });
            }
        }
        [HttpGet]
        [Authorize(Roles = "ROLE_CUSTOMER,ROLE_ADMIN,ROLE_STAFF")]
        public async Task<IActionResult> TourBooked(int bookingId)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                // Handle the case where the username is not available.
                // This might mean redirecting to login or returning an error.
                return RedirectToAction("Login");
            }
            var customer = await _context.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME != null && k.ORACLE_USERNAME.ToUpper() == username.ToUpper());
            if (customer == null)
            {
                return RedirectToAction("Login");
            }
            await UpdateDepartedBookingsToCompletedAsync();
            var booking = await _context.DatTours
                .Where(dt => dt.MaDatTour == bookingId && dt.MaKhachHang == customer.MaKhachHang)
                .Include(dt => dt.Tour)
                .Include(dt => dt.HoaDon)
                .FirstOrDefaultAsync();
            if (booking == null || booking.Tour == null)
            {
                return NotFound();
            }
            var tour = booking.Tour;
            var myTourItem = new MyTourItem
            {
                TourId = tour.MaTour,
                BookingId = booking.MaDatTour,
                CheckoutId = booking.HoaDon?.MaHoaDon ?? 0,
                BookingStatus = booking.TrangThaiDat == "Hoàn thành" ? "f" :
                                booking.TrangThaiDat == "Đã xác nhận" && tour.ThoiGian > DateTime.Now ? "y" :
                                booking.TrangThaiDat == "Đã xác nhận" && tour.ThoiGian <= DateTime.Now ? "f" :
                                booking.TrangThaiDat == "Đã hủy" ? "c" : "b", // 'b' for pending, 'y' for upcoming, 'f' for finished, 'c' for cancelled
                Title = tour.TieuDe ?? string.Empty,
                Description = tour.MoTa ?? string.Empty,
                Destination = tour.NoiDen ?? tour.NoiKhoiHanh ?? tour.ThanhPho ?? string.Empty,
                Time = tour.ThoiGian?.ToString("yyyy-MM-dd") ?? string.Empty,
                NumAdults = booking.SoNguoiLon ?? 0,
                NumChildren = booking.SoTreEm ?? 0,
                TotalPrice = booking.TongTien ?? 0,
                FullName = customer.HoTen,
                Email = customer.Email,
                PhoneNumber = customer.SoDienThoai,
                Address = customer.DiaChi,
                StartDate = tour.ThoiGian,
                EndDate = tour.ThoiGian?.AddDays(3), // Assuming a default tour duration of 3 days
                PriceAdult = tour.GiaNguoiLon ?? 0,
                PriceChild = tour.GiaTreEm ?? 0,
                IsPaid = booking.HoaDon?.TrangThai == "Đã thanh toán"
            };
            var model = new TourBookedViewModel
            {
                TourBooked = myTourItem,
                BookingId = booking.MaDatTour,
                HideCancelButton = (myTourItem.BookingStatus == "c" || myTourItem.BookingStatus == "f") // Hide if cancelled or finished
            };
            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "ROLE_CUSTOMER,ROLE_ADMIN,ROLE_STAFF")]
        public async Task<IActionResult> CancelBooking(int bookingId)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                // Handle the case where the username is not available.
                // This might mean redirecting to login or returning an error.
                return RedirectToAction("Login");
            }
            var customer = await _context.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME != null && k.ORACLE_USERNAME.ToUpper() == username.ToUpper());
            if (customer == null)
            {
                return RedirectToAction("Login");
            }
            await UpdateDepartedBookingsToCompletedAsync();
            var booking = await _context.DatTours
                .Include(dt => dt.HoaDon)
                .Where(dt => dt.MaDatTour == bookingId && dt.MaKhachHang == customer.MaKhachHang)
                .FirstOrDefaultAsync();
            if (booking == null)
            {
                return NotFound();
            }
            if (booking.HoaDon != null && IsInvoicePaid(booking.HoaDon.TrangThai))
            {
                TempData["ErrorMessage"] = "Không thể hủy tour đã thanh toán.";
                return RedirectToAction("MyTour");
            }
            if (booking.TrangThaiDat != "Đã hủy" && booking.TrangThaiDat != "Hoàn thành")
            {
                booking.TrangThaiDat = "Đã hủy";
                _context.DatTours.Update(booking);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Tour đã được hủy thành công.";
            }
            else
            {
                TempData["ErrorMessage"] = "Không thể hủy tour này.";
            }
            return RedirectToAction("MyTour");
        }
        [HttpGet]
        [Authorize(Roles = "ROLE_CUSTOMER,ROLE_ADMIN,ROLE_STAFF")]
        public async Task<IActionResult> Payment(int bookingId)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login");
            }
            var customer = await _dbContext.KhachHangs
                .FirstOrDefaultAsync(k => k.ORACLE_USERNAME.ToUpper() == username.ToUpper());
            if (customer == null)
            {
                return RedirectToAction("Login");
            }
            var booking = await _dbContext.DatTours
                .Include(b => b.Tour)
                .Include(b => b.HoaDon)
                .FirstOrDefaultAsync(b => b.MaDatTour == bookingId && b.MaKhachHang == customer.MaKhachHang);
            if (booking == null || booking.HoaDon == null || booking.Tour == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đặt tour hoặc hóa đơn";
                return RedirectToAction("MyTour");
            }
            if (IsCancellationStatus(booking.TrangThaiDat))
            {
                TempData["ErrorMessage"] = "Đơn đặt đã hủy không thể thanh toán.";
                return RedirectToAction("MyTour");
            }
            bool isValid = false;
            string payloadJson = booking.HoaDon.Payload ?? string.Empty;
            string signature = booking.HoaDon.ChuKySo ?? string.Empty;
            if (!string.IsNullOrEmpty(payloadJson) && !string.IsNullOrEmpty(signature))
            {
                isValid = _rsaService.Verify(payloadJson, signature);
            }
            else
            {
                isValid = false;
            }
            var model = new InvoiceViewModel
            {
                MaHoaDon = booking.HoaDon.MaHoaDon,
                NgayXuat = booking.HoaDon.NgayXuat,
                SoTien = booking.HoaDon.SoTien,
                TrangThai = booking.HoaDon.TrangThai,
                IsSignatureValid = isValid,
                TenTour = booking.Tour.TieuDe,
                NgayKhoiHanh = booking.Tour.ThoiGian,
                SoNguoiLon = booking.SoNguoiLon,
                SoTreEm = booking.SoTreEm,
                TenKhachHang = customer.HoTen,
                Email = customer.Email,
                SoDienThoai = customer.SoDienThoai,
                DiaChi = customer.DiaChi,
                PaymentMethod = booking.HoaDon.PhuongThucThanhToan
            };
            ViewBag.BookingId = booking.MaDatTour;
            return View(model);
        }
        [HttpPost]
        [Authorize(Roles = "ROLE_CUSTOMER,ROLE_ADMIN,ROLE_STAFF")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int bookingId, string? paymentMethod)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login");
            }
            var customer = await _context.KhachHangs
                .FirstOrDefaultAsync(k => k.ORACLE_USERNAME.ToUpper() == username.ToUpper());
            if (customer == null)
            {
                return RedirectToAction("Login");
            }
            var booking = await _context.DatTours
                .Include(b => b.HoaDon)
                .FirstOrDefaultAsync(b => b.MaDatTour == bookingId && b.MaKhachHang == customer.MaKhachHang);
            if (booking == null || booking.HoaDon == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đặt tour hoặc hóa đơn";
                return RedirectToAction("MyTour");
            }
            if (IsCancellationStatus(booking.TrangThaiDat))
            {
                TempData["ErrorMessage"] = "Đơn đặt đã hủy không thể thanh toán.";
                return RedirectToAction("TourBooked", new { bookingId });
            }
            var chosenMethod = string.IsNullOrWhiteSpace(paymentMethod)
                ? "Thanh toán tại văn phòng"
                : paymentMethod.Trim();
            booking.HoaDon.PhuongThucThanhToan = chosenMethod;
            if (!IsInvoicePaid(booking.HoaDon.TrangThai))
            {
                if (booking.TrangThaiDat != "Đã xác nhận" && booking.TrangThaiDat != "Hoàn thành" && booking.TrangThaiDat != "Đã hủy")
                {
                    booking.TrangThaiDat = "Chờ xác nhận";
                }
                booking.HoaDon.TrangThai = "Đã thanh toán";
                _context.DatTours.Update(booking);
                _context.HoaDons.Update(booking.HoaDon);
                await _context.SaveChangesAsync();
                try
                {
                    var hoaDon = booking.HoaDon;
                    var signer = await _dbContext.NhanViens.FirstOrDefaultAsync(n => n.VaiTro != null && n.VaiTro.ToUpper() == "ADMIN");
                    if (signer == null)
                    {
                        signer = await _dbContext.NhanViens.FirstOrDefaultAsync(n => n.ORACLE_USERNAME != null && n.ORACLE_USERNAME.ToUpper() == "ADMIN");
                    }
                    var signerName = signer?.HoTen ?? "Người quản lý";
                    var pdfBytes = CreateInvoicePdf(hoaDon, booking, booking.Tour, await _context.KhachHangs.FirstOrDefaultAsync(k => k.MaKhachHang == booking.MaKhachHang), signerName);
                    var invoicesDir = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "invoices");
                    Directory.CreateDirectory(invoicesDir);
                    var filePath = System.IO.Path.Combine(invoicesDir, $"HoaDon_{hoaDon.MaHoaDon}.pdf");
                    await System.IO.File.WriteAllBytesAsync(filePath, pdfBytes);
                    TempData["SuccessMessage"] = "Thanh toán thành công! Hóa đơn đã được tạo.";
                    TempData["InvoiceUrl"] = $"/invoices/HoaDon_{hoaDon.MaHoaDon}.pdf";
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to generate/save invoice PDF after payment: " + ex.ToString());
                    TempData["SuccessMessage"] = "Thanh toán thành công! Nhưng không thể tạo hóa đơn PDF.";
                }
            }
            else
            {
                _context.HoaDons.Update(booking.HoaDon);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("TourBooked", new { bookingId = bookingId });
        }
        [HttpGet]
        [Authorize(Roles = "ROLE_CUSTOMER,ROLE_ADMIN,ROLE_STAFF")]
        public async Task<IActionResult> DownloadInvoicePdf(int hoaDonId)
        {
            var hoaDon = await _context.HoaDons
                .Include(h => h.DatTour)
                    .ThenInclude(d => d.Tour)
                .Include(h => h.DatTour)
                    .ThenInclude(d => d.KhachHang)
                .FirstOrDefaultAsync(h => h.MaHoaDon == hoaDonId);
            if (hoaDon == null)
            {
                return NotFound();
            }
            var booking = hoaDon.DatTour;
            var tour = booking?.Tour;
            var customer = booking?.KhachHang;
            // Try to get an admin/staff signer name from NhanViens; fallback to a default
            var signer = await _dbContext.NhanViens.FirstOrDefaultAsync(n => n.VaiTro != null && n.VaiTro.ToUpper() == "ADMIN");
            if (signer == null)
            {
                signer = await _dbContext.NhanViens.FirstOrDefaultAsync(n => n.ORACLE_USERNAME != null && n.ORACLE_USERNAME.ToUpper() == "ADMIN");
            }
            var signerName = signer?.HoTen ?? "Người quản lý";
            try
            {
                var pdfBytes = CreateInvoicePdf(hoaDon, booking, tour, customer, signerName);
                var fileName = $"HoaDon_{hoaDon.MaHoaDon}.pdf";
                // Return as proper PDF
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DownloadInvoicePdf] ERROR: {ex.Message}");
                Console.WriteLine($"[DownloadInvoicePdf] StackTrace: {ex.StackTrace}");
                return StatusCode(500, $"Không thể tạo file: {ex.Message}");
            }
        }
        [HttpGet]
        [Authorize(Roles = "ROLE_CUSTOMER,ROLE_ADMIN,ROLE_STAFF")]
        public async Task<IActionResult> PrintInvoice(int hoaDonId)
        {
            var hoaDon = await _context.HoaDons
                .Include(h => h.DatTour)
                    .ThenInclude(d => d.Tour)
                .Include(h => h.DatTour)
                    .ThenInclude(d => d.KhachHang)
                .FirstOrDefaultAsync(h => h.MaHoaDon == hoaDonId);
            if (hoaDon == null)
                return NotFound();
            var booking = hoaDon.DatTour;
            var tour = booking?.Tour;
            var customer = booking?.KhachHang;
            var model = new InvoiceViewModel
            {
                MaHoaDon = hoaDon.MaHoaDon,
                NgayXuat = hoaDon.NgayXuat,
                SoTien = hoaDon.SoTien,
                TrangThai = hoaDon.TrangThai,
                IsSignatureValid = true,
                TenTour = tour?.TieuDe,
                NgayKhoiHanh = tour?.ThoiGian,
                SoNguoiLon = booking?.SoNguoiLon,
                SoTreEm = booking?.SoTreEm,
                TenKhachHang = customer?.HoTen,
                Email = customer?.Email,
                SoDienThoai = customer?.SoDienThoai,
                DiaChi = customer?.DiaChi,
                PaymentMethod = hoaDon.PhuongThucThanhToan
            };
            return View("PrintInvoice", model);
        }
        // Trong file Controllers/customer/CustomerController.cs
        private byte[] CreateInvoicePdf(HoaDon hoaDon, DatTour? booking, Tour? tour, KhachHang? customer, string signerName)
        {
            try
            {
                using var ms = new MemoryStream();
                using (var writer = new PdfWriter(ms))
                using (var pdfDoc = new PdfDocument(writer))
                using (var document = new Document(pdfDoc, iText.Kernel.Geom.PageSize.A4))
                {
                    string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "Arial.ttf");
                    PdfFont font;
                    PdfFont fontBold;
                    if (System.IO.File.Exists(fontPath))
                    {
                        font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H);
                        fontBold = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H);
                    }
                    else
                    {
                        font = PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA);
                        fontBold = PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA_BOLD);
                    }
                    document.SetMargins(30, 30, 30, 30);
                    // 1. TIÃŠU Äá»€
                    document.Add(new Paragraph("Hóa đơn / INVOICE")
                        .SetFont(fontBold)
                        .SetFontSize(20)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetMarginBottom(10));
                    document.Add(new LineSeparator(new iText.Kernel.Pdf.Canvas.Draw.SolidLine(1f)).SetMarginBottom(15));
                    // 2. THÃ”NG TIN CHUNG
                    Table infoTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1 })).UseAllAvailableWidth();
                    infoTable.AddCell(CreateNoBorderCell($"Mã hóa dơn / Order ID: {hoaDon.MaHoaDon}", fontBold));
                    infoTable.AddCell(CreateNoBorderCell($"Ngày / Date: {(hoaDon.NgayXuat?.ToString("yyyy-MM-dd HH:mm:ss") ?? "")}", font));
                    infoTable.AddCell(CreateNoBorderCell($"Khách hàng / Customer: {(customer?.HoTen ?? "Guest")}", fontBold));
                    var paymentMethodLabel = !string.IsNullOrWhiteSpace(hoaDon.PhuongThucThanhToan)
                        ? hoaDon.PhuongThucThanhToan
                        : (hoaDon.TrangThai != null && hoaDon.TrangThai.IndexOf("Thanh toán", System.StringComparison.OrdinalIgnoreCase) >= 0
                            ? "Chuyển khoản / Online"
                            : "Chưa thanh toán");
                    infoTable.AddCell(CreateNoBorderCell($"Thanh toán / Payment: {paymentMethodLabel}", font));
                    document.Add(infoTable);
                    document.Add(new Paragraph($"Địa chỉ / Address: {(customer?.DiaChi ?? "")} - SDT: {(customer?.SoDienThoai ?? "")}")
                        .SetFont(font)
                        .SetFontSize(10)
                        .SetMarginTop(5)
                        .SetMarginBottom(15));
                    Table productTable = new Table(UnitValue.CreatePercentArray(new float[] { 4, 1.5f, 2, 2.5f })).UseAllAvailableWidth();
                    Color headerBg = new DeviceGray(0.9f);
                    productTable.AddHeaderCell(CreateHeaderCell("Sản phẩm / Product", fontBold, headerBg));
                    productTable.AddHeaderCell(CreateHeaderCell("SL / Qty", fontBold, headerBg).SetTextAlignment(TextAlignment.CENTER));
                    productTable.AddHeaderCell(CreateHeaderCell("Giá / Price", fontBold, headerBg).SetTextAlignment(TextAlignment.RIGHT));
                    productTable.AddHeaderCell(CreateHeaderCell("Thanh toán / Subtotal", fontBold, headerBg).SetTextAlignment(TextAlignment.RIGHT));
                    if ((booking?.SoNguoiLon ?? 0) > 0)
                    {
                        decimal price = tour?.GiaNguoiLon ?? 0;
                        decimal subtotal = (booking?.SoNguoiLon ?? 0) * price;
                        productTable.AddCell(CreateCell($"Giá người lớn - {tour?.TieuDe}", font));
                        productTable.AddCell(CreateCell($"{booking?.SoNguoiLon}", font).SetTextAlignment(TextAlignment.CENTER));
                        productTable.AddCell(CreateCell($"{price:N0}", font).SetTextAlignment(TextAlignment.RIGHT));
                        productTable.AddCell(CreateCell($"{subtotal:N0}", font).SetTextAlignment(TextAlignment.RIGHT));
                    }
                    if ((booking?.SoTreEm ?? 0) > 0)
                    {
                        decimal price = tour?.GiaTreEm ?? 0;
                        decimal subtotal = (booking?.SoTreEm ?? 0) * price;
                        productTable.AddCell(CreateCell($"Giá trẻ em - {tour?.TieuDe}", font));
                        productTable.AddCell(CreateCell($"{booking?.SoTreEm}", font).SetTextAlignment(TextAlignment.CENTER));
                        productTable.AddCell(CreateCell($"{price:N0}", font).SetTextAlignment(TextAlignment.RIGHT));
                        productTable.AddCell(CreateCell($"{subtotal:N0}", font).SetTextAlignment(TextAlignment.RIGHT));
                    }
                    Cell totalLabelCell = new Cell(1, 3)
                        .Add(new Paragraph("Tổng / Total"))
                        .SetFont(fontBold)
                        .SetTextAlignment(TextAlignment.RIGHT)
                        .SetBorder(Border.NO_BORDER);
                    productTable.AddCell(totalLabelCell.SetBorderTop(new SolidBorder(1)));
                    Cell totalValueCell = new Cell()
                        .Add(new Paragraph($"{(hoaDon.SoTien ?? 0):N0} VND"))
                        .SetFont(fontBold)
                        .SetFontSize(12)
                        .SetTextAlignment(TextAlignment.RIGHT)
                        .SetBorderTop(new SolidBorder(1));
                    productTable.AddCell(totalValueCell);
                    document.Add(productTable);
                    // 4. FOOTER
                    document.Add(new Paragraph("\n"));
                    Table footerTable = new Table(UnitValue.CreatePercentArray(new float[] { 1 })).UseAllAvailableWidth();
                    Paragraph signerPara = new Paragraph()
                        .Add(new Text("Người ký / Signed by:\n").SetFont(fontBold))
                        .Add(new Text(signerName.ToUpper() + "\n").SetFont(fontBold))
                        .Add(new Text(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss K")).SetFont(font));
                    Cell signerCell = new Cell()
                        .Add(signerPara)
                        .SetTextAlignment(TextAlignment.RIGHT)
                        .SetBorder(Border.NO_BORDER);
                    footerTable.AddCell(signerCell);
                    document.Add(footerTable);
                }
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PDF Error: {ex.Message}");
                return Array.Empty<byte>();
            }
        }
        private Cell CreateNoBorderCell(string text, PdfFont font)
        {
            return new Cell().Add(new Paragraph(text).SetFont(font).SetFontSize(10)).SetBorder(Border.NO_BORDER);
        }
        private Cell CreateHeaderCell(string text, PdfFont font, Color bgColor)
        {
            return new Cell().Add(new Paragraph(text).SetFont(font).SetFontSize(10))
                .SetBackgroundColor(bgColor)
                .SetPadding(5);
        }
        private Cell CreateCell(string text, PdfFont font)
        {
            return new Cell().Add(new Paragraph(text).SetFont(font).SetFontSize(10))
                .SetPadding(5);
        }
        [HttpGet]
        [Authorize]
        public IActionResult Sessions()
        {
            // Shows the session management UI where users can see active sessions and perform remote logout
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> CheckSession()
        {
            try
            {
                var sessionId = Request.Cookies["USER_SESSION_ID"];
                if (string.IsNullOrEmpty(sessionId))
                {
                    return Json(new { valid = false });
                }
                var sess = await _dbContext.UserSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
                var valid = sess != null && sess.IsActive == "Y";
                return Json(new { valid });
            }
            catch
            {
                return Json(new { valid = false });
            }
        }
        [HttpPost]
        [Authorize(Roles = "ROLE_CUSTOMER,ROLE_ADMIN,ROLE_STAFF")]
        public async Task<IActionResult> VerifyInvoice(IFormFile? invoiceFile)
        {
            try
            {
                if (invoiceFile == null || invoiceFile.Length == 0)
                {
                    return Json(new { success = false, message = "Vui lòng chọn file PDF hóa đơn." });
                }
                var fileName = invoiceFile.FileName;
                var match = System.Text.RegularExpressions.Regex.Match(fileName, @"(\d+)");
                if (!match.Success)
                {
                    return Json(new { success = false, message = "Tên file chưa hợp lệ. File phải chưa mã đơn (VD: HoaDon_123.pdf)" });
                }
                int maHoaDon = int.Parse(match.Value);
                var hoaDon = await _dbContext.HoaDons
                    .AsNoTracking()
                    .FirstOrDefaultAsync(h => h.MaHoaDon == maHoaDon);
                if (hoaDon == null)
                {
                    return Json(new { success = false, message = $"Không tìm thấy #{maHoaDon} trên hệ thống." });
                }
                if (string.IsNullOrEmpty(hoaDon.Payload) || string.IsNullOrEmpty(hoaDon.ChuKySo))
                {
                    return Json(new { success = false, message = "Hóa đơn này chưa được ký hoặc thiếu, sai sót dữ liệu" });
                }
                bool isValid = _rsaService.Verify(hoaDon.Payload, hoaDon.ChuKySo);
                if (isValid)
                {
                    return Json(new
                    {
                        success = true,
                        isValid = true,
                        maHoaDon = hoaDon.MaHoaDon,
                        ngayXuat = hoaDon.NgayXuat?.ToString("dd/MM/yyyy HH:mm"),
                        trangThai = hoaDon.TrangThai,
                        message = "Hóa đơn hợp lệ, Ký số khớp với dữ liệu dưới DB."
                    });
                }
                return Json(new
                {
                    success = true,
                    isValid = false,
                    maHoaDon = hoaDon.MaHoaDon,
                    message = "Canh bao: Chu ky so khong khop! Du lieu co the da bi sua doi."
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Loi he thong: " + ex.Message });
            }
        }
        [HttpGet]
        [Authorize(Roles = "ROLE_CUSTOMER,ROLE_ADMIN,ROLE_STAFF")]
        public IActionResult ChangePassword()
        {
            return View();
        }
        [HttpPost]
        [Authorize(Roles = "ROLE_CUSTOMER,ROLE_ADMIN,ROLE_STAFF")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Vui lòng kiểm tra lại thông tin.";
                return Json(new { success = false, message = firstError });
            }
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return Json(new { success = false, message = "Phiên đăng nhập hết hạn, vui lòng đăng nhập lại." });
            }
            // 1. Xác thực mật khẩu cũ
            var (success, role, errorMessage) = await _authService.ValidateLoginAsync(username, model.OldPassword);
            if (!success)
            {
                return Json(new { success = false, message = errorMessage ?? "Mật khẩu hiện tại không đúng." });
            }
            // 2. Thực hiện đổi mật khẩu
            var (changeSuccess, changeMessage) = await _authService.ChangePasswordAsync(username, model.NewPassword);
            if (changeSuccess)
            {
                return Json(new { success = true, message = "Mật khẩu đã được thay đổi thành công." });
            }
            // 3. Xử lý lỗi từ Oracle
            return Json(new { success = false, message = changeMessage ?? "Lỗi hệ thống khi đổi mật khẩu." });
        }
        private bool IsAjaxRequest()
        {
            if (Request?.Headers == null)
            {
                return false;
            }
            var xhr = Request.Headers["X-Requested-With"].ToString();
            var accept = Request.Headers["Accept"].ToString();
            return string.Equals(xhr, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(accept) && accept.Contains("application/json", StringComparison.OrdinalIgnoreCase));
        }
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            // Tìm user trong bảng KhachHang (hoặc NhanVien nếu cần)
            var user = await _dbContext.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME.ToUpper() == model.Username.ToUpper());
            if (user == null || string.IsNullOrEmpty(user.Email))
            {
                // Bảo mật: Không thông báo chi tiết user không tồn tại
                ModelState.AddModelError("", "Không tìm thấy thông tin tài khoản hoặc tài khoản chưa có email.");
                return View(model);
            }
            // Tạo OTP
            var otp = new Random().Next(100000, 999999).ToString();
            var cacheKey = $"OTP_WEB_{model.Username.ToUpper()}";
            // Lưu OTP vào Cache 3 phút
            _cache.Set(cacheKey, otp, TimeSpan.FromMinutes(3));
            // Gửi Email
            try
            {
                await _emailService.SendEmailAsync(user.Email, "Mã xác thực Quên mật khẩu",
                    $"<h3>Mã xác thực của bạn là: <b style='color:red'>{otp}</b></h3><p>Có hiệu lực trong 3 phút.</p>");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi gửi email: " + ex.Message);
                return View(model);
            }
            // Chuyển sang bước nhập OTP
            return RedirectToAction("VerifyOtp", new { username = model.Username });
        }
        [HttpGet]
        [AllowAnonymous]
        public IActionResult VerifyOtp(string username)
        {
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login");
            return View(new VerifyOtpViewModel { Username = username });
        }
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult VerifyOtp(VerifyOtpViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var cacheKey = $"OTP_WEB_{model.Username.ToUpper()}";
            if (_cache.TryGetValue(cacheKey, out string storedOtp))
            {
                if (storedOtp == model.Otp)
                {
                    // OTP đúng -> Cho phép đổi mật khẩu
                    // Dùng TempData để đánh dấu là đã verify thành công (ngăn truy cập trực tiếp bước 3)
                    TempData["VerifiedUser"] = model.Username;
                    TempData["CurrentOtp"] = model.Otp; // Truyền OTP sang bước sau để check lại lần cuối nếu cần
                    return RedirectToAction("ResetPassword");
                }
            }
            ModelState.AddModelError("", "Mã xác thực không đúng hoặc đã hết hạn.");
            return View(model);
        }
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword()
        {
            // Kiểm tra xem đã qua bước verify chưa
            if (TempData["VerifiedUser"] == null)
            {
                return RedirectToAction("ForgotPassword");
            }
            var username = TempData["VerifiedUser"].ToString();
            var otp = TempData["CurrentOtp"]?.ToString(); // Giữ lại OTP để submit
            // Cần giữ lại TempData cho lần POST tiếp theo
            TempData.Keep("VerifiedUser");
            TempData.Keep("CurrentOtp");
            return View(new ResetPasswordViewModel { Username = username, Otp = otp });
        }
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            // Check lại cache lần cuối để đảm bảo OTP còn hiệu lực và quy trình đúng
            var cacheKey = $"OTP_WEB_{model.Username.ToUpper()}";
            if (!_cache.TryGetValue(cacheKey, out string storedOtp) || storedOtp != model.Otp)
            {
                ModelState.AddModelError("", "Phiên giao dịch đã hết hạn. Vui lòng thực hiện lại.");
                return View(model);
            }
            // Thực hiện đổi mật khẩu trong Oracle
            var (success, message) = await _authService.ChangePasswordAsync(model.Username, model.NewPassword);
            if (success)
            {
                _cache.Remove(cacheKey); // Xóa OTP
                TempData["SuccessMessage"] = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại.";
                return RedirectToAction("Login");
            }
            ModelState.AddModelError("", message);
            return View(model);
        }

        private static bool IsInvoicePaid(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            return status.Equals("Đã thanh toán", StringComparison.OrdinalIgnoreCase)
                || status.Equals("Hoàn tất", StringComparison.OrdinalIgnoreCase);
        }
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForceChangePassword(string? username)
        {
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login");

            var model = new ForceChangePasswordViewModel { Username = username };
            return View(model);
        }

        // [MỚI] POST: Xử lý đổi mật khẩu bắt buộc
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceChangePassword(ForceChangePasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var (success, message) = await _authService.ForceChangePasswordAsync(model.Username, model.OldPassword, model.NewPassword);

            if (success)
            {
                TempData["SuccessMessage"] = message;
                return RedirectToAction("Login");
            }

            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }
    }
}
