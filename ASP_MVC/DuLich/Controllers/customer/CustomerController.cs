using DuLich.Models;
using DuLich.Services;
using DuLich.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace DuLich.Controllers
{
    public class CustomerController : BaseController
    {
        private readonly OracleAuthService _authService;
        private readonly RSAService _rsaService;
        private readonly ApplicationDbContext _dbContext;

        public CustomerController(OracleAuthService authService, ApplicationDbContext context, RSAService rsaService) : base(context)
        {
            _authService = authService;
            _rsaService = rsaService;
            _dbContext = context;
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

        [HttpPost]
        public async Task<IActionResult> Login(LoginModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var (success, role) = await _authService.ValidateLoginAsync(model.Username, model.Password);
            Console.WriteLine($"ValidateLoginAsync returned success={success}, role={role} for user={model.Username}");

            if (success && role == "ROLE_CUSTOMER")
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, model.Username),
                    new Claim(ClaimTypes.Role, "ROLE_CUSTOMER")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties();

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);
                Console.WriteLine("SignInAsync completed for user: " + model.Username);

                // Create a persistent session record in DB for centralized session management
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
                            UserType = "CUSTOMER",
                            DeviceType = "WEB",
                            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                            DeviceInfo = Request.Headers["User-Agent"].ToString(),
                            IsActive = "Y",
                            LoginTime = DateTime.UtcNow,
                            LastActivity = DateTime.UtcNow
                        };

                        // Remove any previous sessions for this user that share the same device type
                        // This keeps sessions on other device types (e.g. MOBILE) intact
                        var prev = _dbContext.UserSessions
                            .Where(s => s.UserId == customer.MaKhachHang && s.DeviceType == userSession.DeviceType)
                            .ToList();
                        if (prev.Any())
                        {
                            _dbContext.UserSessions.RemoveRange(prev);
                        }

                        _dbContext.UserSessions.Add(userSession);
                        await _dbContext.SaveChangesAsync();

                        // store session id in cookie for later validation if needed
                        var cookieOptions = new Microsoft.AspNetCore.Http.CookieOptions
                        {
                            HttpOnly = true,
                            Secure = Request.IsHttps,
                            // Browsers require SameSite=None to be paired with Secure; to avoid the cookie being dropped on HTTP during local development,
                            // use Lax when not using HTTPS and None when using HTTPS.
                            SameSite = Request.IsHttps ? Microsoft.AspNetCore.Http.SameSiteMode.None : Microsoft.AspNetCore.Http.SameSiteMode.Lax
                        };
                        Response.Cookies.Append("USER_SESSION_ID", sessionId, cookieOptions);
                        Console.WriteLine($"Set USER_SESSION_ID cookie={sessionId} Secure={cookieOptions.Secure} SameSite={cookieOptions.SameSite}");
                    }
                }
                catch (Exception ex)
                {
                    // log but don't fail login if session creation fails
                    Console.WriteLine("Failed to create user session: " + ex.Message);
                }

                return RedirectToAction("Index", "Customer");
            }

            ModelState.AddModelError(string.Empty, "T�n dang nh?p ho?c m?t kh?u kh�ng d�ng");
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
            var q = _context.Tours.AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var k = keyword.Trim();
                q = q.Where(t => (t.TieuDe != null && EF.Functions.Like(t.TieuDe, $"%{k}%"))
                                 || (t.MoTa != null && EF.Functions.Like(t.MoTa, $"%{k}%")));
            }

            if (!string.IsNullOrWhiteSpace(destination))
            {
                var d = destination.Trim();
                q = q.Where(t => (t.NoiDen != null && EF.Functions.Like(t.NoiDen, $"%{d}%"))
                                 || (t.NoiKhoiHanh != null && EF.Functions.Like(t.NoiKhoiHanh, $"%{d}%"))
                                 || (t.ThanhPho != null && EF.Functions.Like(t.ThanhPho, $"%{d}%")));
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

            var tours = await q.OrderBy(t => t.MaTour).Take(20).ToListAsync();
            var model = new CustomerHomeViewModel();

            foreach (var t in tours)
            {
                var imageIds = await _context.AnhTours
                    .Where(a => a.MaTour == t.MaTour)
                    .OrderBy(a => a.MaAnh)
                    .Select(a => a.MaAnh)
                    .ToListAsync();

                var rating = await _context.DanhGiaTours
                    .Where(d => d.MaTour == t.MaTour)
                    .AverageAsync(d => (decimal?)d.SoSao) ?? 0;

                model.Tours.Add(new TourItem
                {
                    MaTour = t.MaTour,
                    Title = t.TieuDe ?? string.Empty,
                    Destination = t.NoiDen ?? t.NoiKhoiHanh ?? t.ThanhPho ?? string.Empty,
                    Time = t.ThoiGian?.ToString("yyyy-MM-dd") ?? string.Empty,
                    PriceAdult = t.GiaNguoiLon ?? 0,
                    Images = imageIds.Select(id => $"/api/image/{id}").ToList(),
                    Rating = rating
                });
            }

            model.PopularTours = model.Tours.Take(4).ToList();

            return View("Home", model);
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var (success, message) = await _authService.RegisterCustomerAsync(
                model.Username,
                model.Password,
                model.HoTen,
                model.Email,
                model.SoDienThoai,
                model.DiaChi);

            if (success)
            {
                TempData["SuccessMessage"] = message;
                return RedirectToAction("Login");
            }

            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            // Invalidate session in DB if present
            try
            {
                // Prefer removing all session rows for the current user so that other clients
                // (mobile/web) are freed immediately and DB session limits are not hit.
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
                        // fallback: remove by cookie if we couldn't resolve user id
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
                    // no authenticated principal: try cookie-based removal
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
            // Use GUID without dashes for compactness
            return Guid.NewGuid().ToString("N");
        }

        [HttpGet]
        [Authorize(Roles = "ROLE_CUSTOMER,ROLE_ADMIN,ROLE_STAFF")]
        public async Task<IActionResult> MyTour()
        {
            var username = User.Identity.Name;
            var customer = await _context.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME.ToUpper() == username.ToUpper());

            if (customer == null)
            {
                return RedirectToAction("Login"); // Redirect to login if customer not found
            }

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

                var rating = await _context.DanhGiaTours
                    .Where(d => d.MaTour == tour.MaTour)
                    .AverageAsync(d => (decimal?)d.SoSao) ?? 0;

                string bookingStatusChar = "b"; // Default to pending
                if (booking.TrangThaiDat == "�� x�c nh?n" && tour.ThoiGian > DateTime.Now)
                {
                    bookingStatusChar = "y"; // Upcoming
                }
                else if (booking.TrangThaiDat == "�� x�c nh?n" && tour.ThoiGian <= DateTime.Now)
                {
                    bookingStatusChar = "f"; // Finished
                }
                else if (booking.TrangThaiDat == "�� h?y")
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
                    Rating = rating,
                    IsPaid = booking.HoaDon?.TrangThai == "�� thanh to�n"
                });
            }

            // Fetch popular tours (similar logic to Index action)
            var popularToursQuery = _context.Tours.AsQueryable();
            var popularTours = await popularToursQuery.OrderBy(t => t.MaTour).Take(4).ToListAsync();

            foreach (var t in popularTours)
            {
                var imageIds = await _context.AnhTours
                    .Where(a => a.MaTour == t.MaTour)
                    .OrderBy(a => a.MaAnh)
                    .Select(a => a.MaAnh)
                    .ToListAsync();

                var rating = await _context.DanhGiaTours
                    .Where(d => d.MaTour == t.MaTour)
                    .AverageAsync(d => (decimal?)d.SoSao) ?? 0;

                model.PopularTours.Add(new TourItem
                {
                    MaTour = t.MaTour,
                    Title = t.TieuDe ?? string.Empty,
                    Destination = t.NoiDen ?? t.NoiKhoiHanh ?? t.ThanhPho ?? string.Empty,
                    Time = t.ThoiGian?.ToString("yyyy-MM-dd") ?? string.Empty,
                    PriceAdult = t.GiaNguoiLon ?? 0,
                    Images = imageIds.Select(id => $"/api/image/{id}").ToList(),
                    Rating = rating
                });
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> TourDetail(int id)
        {
            var tour = await _context.Tours.FindAsync(id);

            if (tour == null)
            {
                return NotFound();
            }

            var model = new TourDetailViewModel
            {
                MaTour = tour.MaTour,
                TenTour = tour.TieuDe ?? "Chua c� t�n",
                MoTa = tour.MoTa,
                DiemKhoiHanh = tour.NoiKhoiHanh ?? "Chua x�c d?nh",
                DiemDen = tour.NoiDen ?? tour.ThanhPho ?? "Chua x�c d?nh",
                NgayKhoiHanh = tour.ThoiGian ?? DateTime.Now,
                NgayKetThuc = tour.ThoiGian?.AddDays(5) ?? DateTime.Now.AddDays(5), // Gi? s? tour k�o d�i 5 ng�y
                Gia = tour.GiaNguoiLon ?? 0,
                SoLuong = tour.SoLuong ?? 0
            };

            ViewBag.ImageIds = await _context.AnhTours
                .Where(a => a.MaTour == id)
                .OrderBy(a => a.MaAnh)
                .Select(a => a.MaAnh)
                .ToListAsync();

            ViewBag.Rating = await _context.DanhGiaTours
                .Where(d => d.MaTour == id)
                .AverageAsync(d => (decimal?)d.SoSao) ?? 0;

            ViewBag.RelatedTours = await _context.Tours
                .Where(t => t.MaTour != id && (t.NoiDen == model.DiemDen || t.ThanhPho == model.DiemDen))
                .OrderBy(t => t.MaTour)
                .Take(3)
                .Select(t => new TourDetailViewModel
                {
                    MaTour = t.MaTour,
                    TenTour = t.TieuDe ?? "Chua c� t�n",
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
            if (tour == null)
            {
                return NotFound();
            }

            var username = User.Identity.Name;
            var customer = await _context.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME.ToUpper() == username.ToUpper());

            var model = new CreateBookingViewModel
            {
                TourId = tour.MaTour,
                TourTitle = tour.TieuDe,
                StartDate = tour.ThoiGian,
                PriceAdult = tour.GiaNguoiLon ?? 0,
                PriceChild = tour.GiaTreEm ?? 0,
                AvailableSlots = tour.SoLuong ?? 0,
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
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var tour = await _context.Tours.FindAsync(model.TourId);
            if (tour == null) return NotFound();

            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var customer = await _context.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME.ToUpper() == username.ToUpper());
            if (customer == null) return Unauthorized();

            // Kiểm tra số lượng chỗ (như cũ)
            var totalQuantity = model.NumAdults + model.NumChildren;
            if (tour.SoLuong.HasValue && totalQuantity > tour.SoLuong.Value)
            {
                ModelState.AddModelError(string.Empty, "Số lượng người đặt vượt quá số chỗ còn trống");
                return View(model);
            }

            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    // 1. TẠO DỮ LIỆU ĐẶT TOUR
                    var booking = new DatTour
                    {
                        MaTour = model.TourId,
                        MaKhachHang = customer.MaKhachHang,
                        NgayDat = DateTime.Now,
                        SoNguoiLon = model.NumAdults,
                        SoTreEm = model.NumChildren,
                        TongTien = (model.NumAdults * (tour.GiaNguoiLon ?? 0)) + (model.NumChildren * (tour.GiaTreEm ?? 0)),
                        TrangThaiDat = "Chưa xác nhận", // Logic PHP để là 'Chưa xác nhận'
                        TrangThaiThanhToan = "Chưa thanh toán",
                        YeuCauDacBiet = model.SpecialRequest
                    };

                    _context.DatTours.Add(booking);
                    await _context.SaveChangesAsync(); // Lưu để lấy MaDatTour

                    // 2. TẠO HÓA ĐƠN (Lưu trước để lấy MaHoaDon và NgayXuat chuẩn)
                    // Kiểm tra xem trigger có tự tạo hóa đơn không, nếu chưa thì tạo thủ công
                    var hoaDon = await _context.HoaDons.FirstOrDefaultAsync(h => h.MaDatTour == booking.MaDatTour);

                    if (hoaDon == null)
                    {
                        hoaDon = new HoaDon
                        {
                            MaDatTour = booking.MaDatTour,
                            NgayXuat = DateTime.Now,
                            SoTien = booking.TongTien,
                            TrangThai = "Chưa thanh toán"
                        };
                        _context.HoaDons.Add(hoaDon);
                        await _context.SaveChangesAsync(); // Lưu để lấy MaHoaDon
                    }

                    // 3. TẠO PAYLOAD JSON (ĐỒNG BỘ VỚI PHP/FLUTTER)
                    // Cấu trúc này khớp hoàn toàn với file create_booking.php bạn đã gửi
                    var payloadObj = new
                    {
                        maHoaDon = hoaDon.MaHoaDon,
                        maDatTour = booking.MaDatTour,
                        maKhachHang = booking.MaKhachHang,
                        soTien = (double)(hoaDon.SoTien ?? 0), // Ép kiểu double cho giống JSON number
                        ngayXuat = hoaDon.NgayXuat?.ToString("yyyy-MM-dd HH:mm:ss"), // Định dạng ngày giống Oracle TO_CHAR
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() // Timestamp hiện tại
                    };

                    // Chuyển Object sang chuỗi JSON
                    string payloadJson = System.Text.Json.JsonSerializer.Serialize(payloadObj);

                    // 4. KÝ SỐ
                    // Ký chuỗi JSON vừa tạo
                    string signature = _rsaService.Sign(payloadJson);

                    // 5. CẬP NHẬT LẠI VÀO DATABASE
                    hoaDon.Payload = payloadJson; // Lưu JSON gốc vào cột Payload
                    hoaDon.ChuKySo = signature;   // Lưu chữ ký

                    _context.HoaDons.Update(hoaDon);
                    await _context.SaveChangesAsync();

                    transaction.Commit();

                    // Chuyển sang trang thanh toán
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
            var username = User.Identity.Name;
            var customer = await _context.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME.ToUpper() == username.ToUpper());

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
                DiaChi = customer.DiaChi,
                Avatar = customer.QR_CODE // Assuming QR_CODE is used for avatar or a placeholder - Added comment to force re-compilation
            };

            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = "ROLE_CUSTOMER,ROLE_ADMIN,ROLE_STAFF")]
        public async Task<IActionResult> TourBooked(int bookingId)
        {
            var username = User.Identity.Name;
            var customer = await _context.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME.ToUpper() == username.ToUpper());

            if (customer == null)
            {
                return RedirectToAction("Login");
            }

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
                BookingStatus = booking.TrangThaiDat == "�� x�c nh?n" && tour.ThoiGian > DateTime.Now ? "y" :
                                booking.TrangThaiDat == "�� x�c nh?n" && tour.ThoiGian <= DateTime.Now ? "f" :
                                booking.TrangThaiDat == "�� h?y" ? "c" : "b", // 'b' for pending, 'y' for upcoming, 'f' for finished, 'c' for cancelled
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
                IsPaid = booking.HoaDon?.TrangThai == "�� thanh to�n"
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
            var username = User.Identity.Name;
            var customer = await _context.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME.ToUpper() == username.ToUpper());

            if (customer == null)
            {
                return RedirectToAction("Login");
            }

            var booking = await _context.DatTours
                .Where(dt => dt.MaDatTour == bookingId && dt.MaKhachHang == customer.MaKhachHang)
                .FirstOrDefaultAsync();

            if (booking == null)
            {
                return NotFound();
            }

            // Only allow cancellation if the booking is not already cancelled or finished
            if (booking.TrangThaiDat != "�� h?y" && booking.TrangThaiDat != "�� ho�n th�nh")
            {
                booking.TrangThaiDat = "�� h?y";
                _context.DatTours.Update(booking);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Tour d� du?c h?y th�nh c�ng.";
            }
            else
            {
                TempData["ErrorMessage"] = "Kh�ng th? h?y tour n�y.";
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
                .Include(b => b.HoaDon) // Entity HoaDon giờ đã có trường Payload
                .FirstOrDefaultAsync(b => b.MaDatTour == bookingId && b.MaKhachHang == customer.MaKhachHang);

            if (booking == null || booking.HoaDon == null || booking.Tour == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đặt tour hoặc hóa đơn";
                return RedirectToAction("MyTour");
            }

            // *** BẮT ĐẦU SỬA ĐỔI LOGIC KIỂM TRA ***

            bool isValid = false;

            // 1. Lấy Payload gốc (JSON) và Chữ ký từ Database
            string payloadJson = booking.HoaDon.Payload ?? string.Empty;
            string signature = booking.HoaDon.ChuKySo ?? string.Empty;

            // 2. Kiểm tra: Chỉ verify khi có đủ dữ liệu
            if (!string.IsNullOrEmpty(payloadJson) && !string.IsNullOrEmpty(signature))
            {
                // Gọi RSAService để verify (Hàm này phải dùng SHA256 và PKCS1 như bạn đã sửa ở RSAService)
                isValid = _rsaService.Verify(payloadJson, signature);
            }
            else
            {
                // Nếu thiếu Payload hoặc Chữ ký -> Coi như không hợp lệ (hoặc chưa ký)
                isValid = false;
            }

            // *** KẾT THÚC SỬA ĐỔI ***

            var model = new InvoiceViewModel
            {
                MaHoaDon = booking.HoaDon.MaHoaDon,
                NgayXuat = booking.HoaDon.NgayXuat,
                SoTien = booking.HoaDon.SoTien,
                TrangThai = booking.HoaDon.TrangThai,

                // Gán kết quả kiểm tra vào đây để View hiển thị
                IsSignatureValid = isValid,

                // Thông tin tour & khách hàng (giữ nguyên)
                TenTour = booking.Tour.TieuDe,
                NgayKhoiHanh = booking.Tour.ThoiGian,
                SoNguoiLon = booking.SoNguoiLon,
                SoTreEm = booking.SoTreEm,
                TenKhachHang = customer.HoTen,
                Email = customer.Email,
                SoDienThoai = customer.SoDienThoai,
                DiaChi = customer.DiaChi
            };

            ViewBag.BookingId = booking.MaDatTour;

            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "ROLE_CUSTOMER,ROLE_ADMIN,ROLE_STAFF")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int bookingId)
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
                TempData["ErrorMessage"] = "Kh�ng t�m th?y th�ng tin d?t tour ho?c h�a don";
                return RedirectToAction("MyTour");
            }

            if (booking.HoaDon.TrangThai != "�� thanh to�n")
            {
                booking.TrangThaiDat = "�� thanh to�n";
                booking.TrangThaiThanhToan = "�� thanh to�n";
                booking.HoaDon.TrangThai = "�� thanh to�n";
                _context.DatTours.Update(booking);
                _context.HoaDons.Update(booking.HoaDon);
                await _context.SaveChangesAsync();
                // After marking paid, generate PDF invoice and save it to wwwroot/invoices
                try
                {
                    var hoaDon = booking.HoaDon;
                    // get signer name
                    var signer = await _dbContext.NhanViens.FirstOrDefaultAsync(n => n.VaiTro != null && n.VaiTro.ToUpper() == "ADMIN");
                    if (signer == null)
                    {
                        signer = await _dbContext.NhanViens.FirstOrDefaultAsync(n => n.ORACLE_USERNAME != null && n.ORACLE_USERNAME.ToUpper() == "ADMIN");
                    }
                    var signerName = signer?.HoTen ?? "Ngu?i qu?n l�";

                    var pdfBytes = CreateInvoicePdf(hoaDon, booking, booking.Tour, await _context.KhachHangs.FirstOrDefaultAsync(k => k.MaKhachHang == booking.MaKhachHang), signerName);

                    var invoicesDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "invoices");
                    Directory.CreateDirectory(invoicesDir);
                    var filePath = Path.Combine(invoicesDir, $"HoaDon_{hoaDon.MaHoaDon}.pdf");
                    await System.IO.File.WriteAllBytesAsync(filePath, pdfBytes);

                    TempData["SuccessMessage"] = "Thanh to�n th�nh c�ng! H�a don d� du?c t?o.";
                    TempData["InvoiceUrl"] = $"/invoices/HoaDon_{hoaDon.MaHoaDon}.pdf";
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to generate/save invoice PDF after payment: " + ex.ToString());
                    TempData["SuccessMessage"] = "Thanh to�n th�nh c�ng! Nhung kh�ng th? t?o h�a don PDF.";
                }
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
            var signerName = signer?.HoTen ?? "Ngu?i qu?n l�";

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
                return StatusCode(500, $"Kh�ng th? t?o file: {ex.Message}");
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
                DiaChi = customer?.DiaChi
            };

            return View("PrintInvoice", model);
        }

        private byte[] CreateInvoicePdf(HoaDon hoaDon, DatTour? booking, Tour? tour, KhachHang? customer, string signerName)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    // Create PDF document
                    Document document = new Document(PageSize.A4, 50, 50, 50, 50);
                    PdfWriter writer = PdfWriter.GetInstance(document, ms);
                    document.Open();

                    // Title
                    Paragraph title = new Paragraph("H�A �ON �?T TOUR", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18));
                    title.Alignment = Element.ALIGN_CENTER;
                    document.Add(title);
                    document.Add(new Paragraph(" "));

                    // Invoice header info
                    PdfPTable headerTable = new PdfPTable(2);
                    headerTable.WidthPercentage = 100;
                    headerTable.AddCell("M� h�a don: " + hoaDon.MaHoaDon);
                    headerTable.AddCell("Ng�y xu?t: " + (hoaDon.NgayXuat?.ToString("dd/MM/yyyy HH:mm:ss") ?? ""));
                    headerTable.AddCell("Tr?ng th�i: " + hoaDon.TrangThai);
                    headerTable.AddCell(" ");
                    document.Add(headerTable);
                    document.Add(new Paragraph(" "));

                    // Customer info section
                    document.Add(new Paragraph("TH�NG TIN KH�CH H�NG", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12)));
                    PdfPTable customerTable = new PdfPTable(2);
                    customerTable.WidthPercentage = 100;
                    customerTable.AddCell("T�n: " + (customer?.HoTen ?? ""));
                    customerTable.AddCell("Email: " + (customer?.Email ?? ""));
                    customerTable.AddCell("�i?n tho?i: " + (customer?.SoDienThoai ?? ""));
                    customerTable.AddCell("�?a ch?: " + (customer?.DiaChi ?? ""));
                    document.Add(customerTable);
                    document.Add(new Paragraph(" "));

                    // Tour details
                    document.Add(new Paragraph("CHI TI?T TOUR", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12)));
                    PdfPTable tourTable = new PdfPTable(2);
                    tourTable.WidthPercentage = 100;
                    tourTable.AddCell("T�n tour: " + (tour?.TieuDe ?? ""));
                    tourTable.AddCell("Ng�y kh?i h�nh: " + (tour?.ThoiGian?.ToString("dd/MM/yyyy") ?? ""));
                    tourTable.AddCell("S? ngu?i l?n: " + (booking?.SoNguoiLon ?? 0));
                    tourTable.AddCell("S? tr? em: " + (booking?.SoTreEm ?? 0));
                    document.Add(tourTable);
                    document.Add(new Paragraph(" "));

                    // Total
                    Paragraph total = new Paragraph($"T?NG TI?N: {(hoaDon.SoTien ?? 0):N0} VN�", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14));
                    total.Alignment = Element.ALIGN_CENTER;
                    document.Add(total);
                    document.Add(new Paragraph(" "));

                    // Signature info
                    var signatureSource = InvoiceSignatureHelper.CreatePayload(booking, hoaDon);
                    byte[] hashBytes;
                    try
                    {
                        hashBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(signatureSource));
                    }
                    catch
                    {
                        hashBytes = new byte[0];
                    }
                    var hashHex = hashBytes.Length > 0 ? BitConverter.ToString(hashBytes).Replace("-", "") : string.Empty;
                    var authCode = hashHex.Length >= 12 ? hashHex.Substring(0, 12) : hashHex;

                    document.Add(new Paragraph("TH�NG TIN X�C TH?C", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12)));
                    PdfPTable signTable = new PdfPTable(2);
                    signTable.WidthPercentage = 100;
                    signTable.AddCell("Ch? k� s?:\n" + (hoaDon.ChuKySo ?? ""));
                    signTable.AddCell("M� x�c th?c: " + authCode);
                    document.Add(signTable);
                    document.Add(new Paragraph(" "));
                    document.Add(new Paragraph("Hash (SHA256): " + hashHex, FontFactory.GetFont(FontFactory.HELVETICA, 9)));
                    document.Add(new Paragraph(" "));

                    // Signature lines
                    document.Add(new Paragraph("K? D?A", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12)));
                    PdfPTable signatureTable = new PdfPTable(2);
                    signatureTable.WidthPercentage = 100;
                    PdfPCell cell1 = new PdfPCell(new Phrase("Ngu?i l?p\n\n\n\n(K� v� ghi r� h? t�n)"));
                    cell1.MinimumHeight = 80;
                    PdfPCell cell2 = new PdfPCell(new Phrase($"Ngu?i k�: {signerName}\n\n\n"));
                    cell2.MinimumHeight = 80;
                    signatureTable.AddCell(cell1);
                    signatureTable.AddCell(cell2);
                    document.Add(signatureTable);

                    document.Close();
                    writer.Close();

                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CreateInvoicePdf] Error: {ex.Message}");
                Console.WriteLine($"[CreateInvoicePdf] StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        // Thay thế hàm GenerateInvoiceHtml cũ bằng hàm này (giống hệt bên API)
        private string GenerateInvoiceHtml(HoaDon hoaDon, DatTour booking, Tour tour, KhachHang customer, string signerName)
        {
            // Tính toán Hash và AuthCode để hiển thị
            var signatureData = InvoiceSignatureHelper.CreatePayload(booking, hoaDon);
            string hashHex = "";
            string authCode = "";

            try
            {
                // Dùng SHA256 hash payload
                byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(signatureData));
                hashHex = BitConverter.ToString(hashBytes).Replace("-", "");
                // Lấy 12 ký tự đầu làm mã xác thực ngắn
                authCode = hashHex.Length >= 12 ? hashHex.Substring(0, 12) : hashHex;
            }
            catch { }

            var total = hoaDon.SoTien ?? 0m;
            var ngayXuat = hoaDon.NgayXuat?.ToString("dd/MM/yyyy HH:mm:ss") ?? "N/A";
            var ngayDi = tour.ThoiGian?.ToString("dd/MM/yyyy") ?? "N/A";

            // Trả về chuỗi HTML chuẩn đẹp
            return $@"
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset='UTF-8'>
        <title>Hóa đơn #{hoaDon.MaHoaDon}</title>
        <style>
            body {{ font-family: Arial, sans-serif; margin: 20px; font-size: 14px; line-height: 1.5; color: #333; }}
            .header {{ text-align: center; margin-bottom: 30px; border-bottom: 2px solid #007AFF; padding-bottom: 10px; }}
            .header h1 {{ color: #007AFF; margin: 0; text-transform: uppercase; }}
            .section-title {{ color: #007AFF; font-weight: bold; margin-top: 20px; border-bottom: 1px solid #ddd; padding-bottom: 5px; }}
            table {{ width: 100%; border-collapse: collapse; margin-top: 10px; }}
            td {{ padding: 8px; vertical-align: top; }}
            .label {{ font-weight: bold; color: #555; width: 140px; }}
            .total-box {{ text-align: right; margin-top: 20px; font-size: 18px; font-weight: bold; color: #d32f2f; }}
            .signature-box {{ margin-top: 30px; background: #f8f9fa; padding: 15px; border: 1px dashed #ccc; border-radius: 8px; font-size: 12px; word-break: break-all; }}
            .footer {{ margin-top: 50px; text-align: center; font-size: 12px; color: #888; }}
            .badge {{ background: #28a745; color: white; padding: 2px 6px; border-radius: 4px; font-size: 11px; }}
        </style>
    </head>
    <body>
        <div class='header'>
            <h1>HÓA ĐƠN ĐIỆN TỬ</h1>
            <p>Mã hóa đơn: <b>#{hoaDon.MaHoaDon}</b> | Ngày xuất: {ngayXuat}</p>
        </div>

        <div class='section-title'>THÔNG TIN KHÁCH HÀNG</div>
        <table>
            <tr><td class='label'>Họ tên:</td><td>{customer.HoTen}</td></tr>
            <tr><td class='label'>Email:</td><td>{customer.Email}</td></tr>
            <tr><td class='label'>Số điện thoại:</td><td>{customer.SoDienThoai}</td></tr>
            <tr><td class='label'>Địa chỉ:</td><td>{customer.DiaChi}</td></tr>
        </table>

        <div class='section-title'>CHI TIẾT DỊCH VỤ</div>
        <table>
            <tr><td class='label'>Tên Tour:</td><td><strong>{tour.TieuDe}</strong></td></tr>
            <tr><td class='label'>Mã Tour:</td><td>#{tour.MaTour}</td></tr>
            <tr><td class='label'>Khởi hành:</td><td>{ngayDi} tại {tour.NoiKhoiHanh}</td></tr>
            <tr><td class='label'>Số lượng:</td><td>{booking.SoNguoiLon} Người lớn, {booking.SoTreEm} Trẻ em</td></tr>
            <tr><td class='label'>Trạng thái:</td><td><span class='badge'>{hoaDon.TrangThai}</span></td></tr>
        </table>

        <div class='total-box'>
            TỔNG THANH TOÁN: {total:N0} VNĐ
        </div>

        <div class='signature-box'>
            <div style='margin-bottom: 5px; color: #007AFF; font-weight: bold;'>THÔNG TIN XÁC THỰC (DIGITAL SIGNATURE)</div>
            <table style='margin:0'>
                <tr><td class='label' style='width:100px'>Mã kiểm tra:</td><td><b>{authCode}</b></td></tr>
                <tr><td class='label' style='width:100px'>Hash (SHA256):</td><td style='font-family:monospace; font-size:10px'>{hashHex}</td></tr>
                <tr><td class='label' style='width:100px'>Chữ ký số:</td><td style='font-family:monospace; font-size:10px'>{hoaDon.ChuKySo}</td></tr>
            </table>
            <p style='margin-top:10px; font-style:italic; color:#666;'>
                * Hóa đơn này được ký số bảo mật. Quý khách có thể sử dụng Mã kiểm tra hoặc upload file PDF để xác thực tính toàn vẹn của hóa đơn.
            </p>
        </div>

        <div class='footer'>
            Cảm ơn quý khách đã sử dụng dịch vụ của DuLich!<br/>
            Hệ thống quản lý tour du lịch trực tuyến.
        </div>
        <script>window.print();</script>
    </body>
    </html>";
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
                    return Json(new { success = false, message = "Vui lòng chọn file PDF." });
                }

                // 1. Lấy ID từ tên file (VD: HoaDon_123.pdf -> 123)
                var fileName = invoiceFile.FileName;
                var match = System.Text.RegularExpressions.Regex.Match(fileName, @"(\d+)");

                if (!match.Success)
                {
                    return Json(new { success = false, message = "Tên file không hợp lệ. Phải chứa mã hóa đơn (VD: HoaDon_123.pdf)" });
                }

                int maHoaDon = int.Parse(match.Value);

                // 2. Truy vấn DB lấy Payload và Chữ ký
                // Quan trọng: Phải lấy cột Payload, vì đó là dữ liệu gốc lúc ký
                var hoaDon = await _dbContext.HoaDons
                    .AsNoTracking() // Không cần track changes
                    .FirstOrDefaultAsync(h => h.MaHoaDon == maHoaDon);

                if (hoaDon == null)
                {
                    return Json(new { success = false, message = $"Không tìm thấy hóa đơn #{maHoaDon} trên hệ thống." });
                }

                if (string.IsNullOrEmpty(hoaDon.Payload) || string.IsNullOrEmpty(hoaDon.ChuKySo))
                {
                    return Json(new { success = false, message = "Hóa đơn này chưa được ký số hoặc thiếu dữ liệu gốc." });
                }

                // 3. Gọi RSA Service để kiểm tra
                // Tham số 1: Payload (JSON chuỗi) lấy từ DB
                // Tham số 2: Chữ ký (Base64) lấy từ DB
                bool isValid = _rsaService.Verify(hoaDon.Payload, hoaDon.ChuKySo);

                // 4. Trả kết quả
                if (isValid)
                {
                    return Json(new
                    {
                        success = true,
                        isValid = true,
                        maHoaDon = hoaDon.MaHoaDon,
                        ngayXuat = hoaDon.NgayXuat?.ToString("dd/MM/yyyy HH:mm"),
                        trangThai = hoaDon.TrangThai,
                        message = "Hóa đơn HỢP LỆ. Chữ ký số khớp hoàn toàn với dữ liệu gốc."
                    });
                }
                else
                {
                    return Json(new
                    {
                        success = true,
                        isValid = false,
                        maHoaDon = hoaDon.MaHoaDon,
                        message = "CẢNH BÁO: Chữ ký số KHÔNG KHỚP! Dữ liệu có thể đã bị sửa đổi."
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }
    }
}





