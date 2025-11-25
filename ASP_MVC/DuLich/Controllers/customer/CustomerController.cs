using DuLich.Models;
using DuLich.Services;
using DuLich.Models.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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

namespace DuLich.Controllers
{
    public class CustomerController : BaseController
    {
        private readonly OracleAuthService _authService;
        private readonly RSAService _rsaService;
        private readonly ApplicationDbContext _dbContext;
        private readonly IWebHostEnvironment _env;

        public CustomerController(OracleAuthService authService, ApplicationDbContext context, RSAService rsaService, IWebHostEnvironment env) : base(context)
        {
            _authService = authService;
            _rsaService = rsaService;
            _dbContext = context;
            _env = env;
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

            ModelState.AddModelError(string.Empty, "T�n dang nh?p v� m?t kh?u chua ch�nh x�c");
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
                    Rating = rating,
                    QR = t.QR ?? string.Empty
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
                NgayKetThuc = tour.ThoiGian?.AddDays(5) ?? DateTime.Now.AddDays(5), // Gi? s? tour k?o d?i 5 ng?y
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
                    TenTour = t.TieuDe ?? "Chua x�c d?nh",
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

            // Ki?m tra s? lu?ng ch? (nhu cu)
            var totalQuantity = model.NumAdults + model.NumChildren;
            if (tour.SoLuong.HasValue && totalQuantity > tour.SoLuong.Value)
            {
                ModelState.AddModelError(string.Empty, "S? lu?ng ngu?i d?t vu?t qu� s? ch? c�n tr?ng");
                return View(model);
            }

            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    // 1. T?O D? LI?U �?T TOUR
                    var booking = new DatTour
                    {
                        MaTour = model.TourId,
                        MaKhachHang = customer.MaKhachHang,
                        NgayDat = DateTime.Now,
                        SoNguoiLon = model.NumAdults,
                        SoTreEm = model.NumChildren,
                        TongTien = (model.NumAdults * (tour.GiaNguoiLon ?? 0)) + (model.NumChildren * (tour.GiaTreEm ?? 0)),
                        TrangThaiDat = "Chua x�c nh?n", // Logic PHP d? l� 'Chua x�c nh?n'
                        TrangThaiThanhToan = "Chua thanh to�n",
                        YeuCauDacBiet = model.SpecialRequest
                    };

                    _context.DatTours.Add(booking);
                    await _context.SaveChangesAsync(); // Luu d? l?y MaDatTour

                    // 2. T?O H�A �ON (Luu tru?c d? l?y MaHoaDon v� NgayXuat chu?n)
                    // Ki?m tra xem trigger c� t? t?o h�a don kh�ng, n?u chua th� t?o th? c�ng
                    var hoaDon = await _context.HoaDons.FirstOrDefaultAsync(h => h.MaDatTour == booking.MaDatTour);

                    if (hoaDon == null)
                    {
                        hoaDon = new HoaDon
                        {
                            MaDatTour = booking.MaDatTour,
                            NgayXuat = DateTime.Now,
                            SoTien = booking.TongTien,
                            TrangThai = "Chua thanh to�n"
                        };
                        _context.HoaDons.Add(hoaDon);
                        await _context.SaveChangesAsync(); // Luu d? l?y MaHoaDon
                    }

                    // 3. T?O PAYLOAD JSON (�?NG B? V?I PHP/FLUTTER)
                    // C?u tr�c n�y kh?p ho�n to�n v?i file create_booking.php b?n d� g?i
                    var payloadObj = new
                    {
                        maHoaDon = hoaDon.MaHoaDon,
                        maDatTour = booking.MaDatTour,
                        maKhachHang = booking.MaKhachHang,
                        soTien = (double)(hoaDon.SoTien ?? 0), // �p ki?u double cho gi?ng JSON number
                        ngayXuat = hoaDon.NgayXuat?.ToString("yyyy-MM-dd HH:mm:ss"), // �?nh d?ng ng�y gi?ng Oracle TO_CHAR
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() // Timestamp hi?n t?i
                    };

                    // Chuy?n Object sang chu?i JSON
                    string payloadJson = System.Text.Json.JsonSerializer.Serialize(payloadObj);

                    // 4. K� S?
                    // K� chu?i JSON v?a t?o
                    string signature = _rsaService.Sign(payloadJson);

                    // 5. C?P NH?T L?I V�O DATABASE
                    hoaDon.Payload = payloadJson; // Luu JSON g?c v�o c?t Payload
                    hoaDon.ChuKySo = signature;   // Luu ch? k�

                    _context.HoaDons.Update(hoaDon);
                    await _context.SaveChangesAsync();

                    transaction.Commit();

                    // Chuy?n sang trang thanh to�n
                    return RedirectToAction("Payment", new { bookingId = booking.MaDatTour });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    ModelState.AddModelError("", "L?i khi d?t tour: " + ex.Message);
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
            if (booking.TrangThaiDat != "�� h?y" && booking.TrangThaiDat != "?? ho?n th?nh")
            {
                booking.TrangThaiDat = "�� h?y";
                _context.DatTours.Update(booking);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Tour d? du?c h?y th?nh c?ng.";
            }
            else
            {
                TempData["ErrorMessage"] = "Kh?ng th? h?y tour n?y.";
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
                .Include(b => b.HoaDon) // Entity HoaDon gi? d� c� tru?ng Payload
                .FirstOrDefaultAsync(b => b.MaDatTour == bookingId && b.MaKhachHang == customer.MaKhachHang);

            if (booking == null || booking.HoaDon == null || booking.Tour == null)
            {
                TempData["ErrorMessage"] = "Kh�ng t�m th?y th�ng tin d?t tour ho?c h�a don";
                return RedirectToAction("MyTour");
            }

            // *** B?T �?U S?A �?I LOGIC KI?M TRA ***

            bool isValid = false;

            // 1. L?y Payload g?c (JSON) v� Ch? k� t? Database
            string payloadJson = booking.HoaDon.Payload ?? string.Empty;
            string signature = booking.HoaDon.ChuKySo ?? string.Empty;

            // 2. Ki?m tra: Ch? verify khi c� d? d? li?u
            if (!string.IsNullOrEmpty(payloadJson) && !string.IsNullOrEmpty(signature))
            {
                // G?i RSAService d? verify (H�m n�y ph?i d�ng SHA256 v� PKCS1 nhu b?n d� s?a ? RSAService)
                isValid = _rsaService.Verify(payloadJson, signature);
            }
            else
            {
                // N?u thi?u Payload ho?c Ch? k� -> Coi nhu kh�ng h?p l? (ho?c chua k�)
                isValid = false;
            }

            // *** K?T TH�C S?A �?I ***

            var model = new InvoiceViewModel
            {
                MaHoaDon = booking.HoaDon.MaHoaDon,
                NgayXuat = booking.HoaDon.NgayXuat,
                SoTien = booking.HoaDon.SoTien,
                TrangThai = booking.HoaDon.TrangThai,

                // G�n k?t qu? ki?m tra v�o d�y d? View hi?n th?
                IsSignatureValid = isValid,

                // Th�ng tin tour & kh�ch h�ng (gi? nguy�n)
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

                    var invoicesDir = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "invoices");
                    Directory.CreateDirectory(invoicesDir);
                    var filePath = System.IO.Path.Combine(invoicesDir, $"HoaDon_{hoaDon.MaHoaDon}.pdf");
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
                    // --- SỬA LỖI Ở ĐÂY ---
                    // Thay vì dùng 'Path.Combine', phải dùng 'System.IO.Path.Combine'
                    string fontPath = System.IO.Path.Combine(_env.WebRootPath, "fonts", "Arial.ttf");
                    // ---------------------

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

                    // 1. TIÊU ĐỀ
                    document.Add(new Paragraph("HÓA ĐƠN / INVOICE")
                        .SetFont(fontBold)
                        .SetFontSize(20)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetMarginBottom(10));

                    document.Add(new LineSeparator(new iText.Kernel.Pdf.Canvas.Draw.SolidLine(1f)).SetMarginBottom(15));

                    // 2. THÔNG TIN CHUNG
                    Table infoTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1 })).UseAllAvailableWidth();

                    infoTable.AddCell(CreateNoBorderCell($"Mã đơn hàng / Order ID: {hoaDon.MaHoaDon}", fontBold));
                    infoTable.AddCell(CreateNoBorderCell($"Ngày / Date: {(hoaDon.NgayXuat?.ToString("yyyy-MM-dd HH:mm:ss") ?? "")}", font));

                    infoTable.AddCell(CreateNoBorderCell($"Khách hàng / Customer: {(customer?.HoTen ?? "Guest")}", fontBold));
                    string paymentMethod = hoaDon.TrangThai?.Contains("Thanh toán") == true ? "Chuyển khoản / Online" : "Chưa thanh toán";
                    infoTable.AddCell(CreateNoBorderCell($"Thanh toán / Payment: {paymentMethod}", font));

                    document.Add(infoTable);

                    document.Add(new Paragraph($"Địa chỉ / Address: {(customer?.DiaChi ?? "")} - SĐT: {(customer?.SoDienThoai ?? "")}")
                        .SetFont(font)
                        .SetFontSize(10)
                        .SetMarginTop(5)
                        .SetMarginBottom(15));

                    // 3. BẢNG SẢN PHẨM
                    Table productTable = new Table(UnitValue.CreatePercentArray(new float[] { 4, 1.5f, 2, 2.5f })).UseAllAvailableWidth();

                    Color headerBg = new DeviceGray(0.9f);
                    productTable.AddHeaderCell(CreateHeaderCell("Sản phẩm / Product", fontBold, headerBg));
                    productTable.AddHeaderCell(CreateHeaderCell("SL / Qty", fontBold, headerBg).SetTextAlignment(TextAlignment.CENTER));
                    productTable.AddHeaderCell(CreateHeaderCell("Đơn giá / Price", fontBold, headerBg).SetTextAlignment(TextAlignment.RIGHT));
                    productTable.AddHeaderCell(CreateHeaderCell("Thành tiền / Subtotal", fontBold, headerBg).SetTextAlignment(TextAlignment.RIGHT));

                    if ((booking?.SoNguoiLon ?? 0) > 0)
                    {
                        decimal price = tour?.GiaNguoiLon ?? 0;
                        decimal subtotal = (booking?.SoNguoiLon ?? 0) * price;

                        productTable.AddCell(CreateCell($"Vé người lớn - {tour?.TieuDe}", font));
                        productTable.AddCell(CreateCell($"{booking?.SoNguoiLon}", font).SetTextAlignment(TextAlignment.CENTER));
                        productTable.AddCell(CreateCell($"{price:N0}", font).SetTextAlignment(TextAlignment.RIGHT));
                        productTable.AddCell(CreateCell($"{subtotal:N0}", font).SetTextAlignment(TextAlignment.RIGHT));
                    }

                    if ((booking?.SoTreEm ?? 0) > 0)
                    {
                        decimal price = tour?.GiaTreEm ?? 0;
                        decimal subtotal = (booking?.SoTreEm ?? 0) * price;

                        productTable.AddCell(CreateCell($"Vé trẻ em - {tour?.TieuDe}", font));
                        productTable.AddCell(CreateCell($"{booking?.SoTreEm}", font).SetTextAlignment(TextAlignment.CENTER));
                        productTable.AddCell(CreateCell($"{price:N0}", font).SetTextAlignment(TextAlignment.RIGHT));
                        productTable.AddCell(CreateCell($"{subtotal:N0}", font).SetTextAlignment(TextAlignment.RIGHT));
                    }

                    Cell totalLabelCell = new Cell(1, 3)
                        .Add(new Paragraph("Tổng cộng / Total"))
                        .SetFont(fontBold)
                        .SetTextAlignment(TextAlignment.RIGHT)
                        .SetBorder(Border.NO_BORDER);

                    productTable.AddCell(totalLabelCell.SetBorderTop(new SolidBorder(1)));

                    Cell totalValueCell = new Cell()
                        .Add(new Paragraph($"{(hoaDon.SoTien ?? 0):N0} VNĐ"))
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

        // --- CÁC HÀM BỔ TRỢ (Helper Methods) ---

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
                    return Json(new { success = false, message = "Vui l�ng ch?n file PDF." });
                }

                // 1. L?y ID t? t�n file (VD: HoaDon_123.pdf -> 123)
                var fileName = invoiceFile.FileName;
                var match = System.Text.RegularExpressions.Regex.Match(fileName, @"(\d+)");

                if (!match.Success)
                {
                    return Json(new { success = false, message = "T�n file kh�ng h?p l?. Ph?i ch?a m� h�a don (VD: HoaDon_123.pdf)" });
                }

                int maHoaDon = int.Parse(match.Value);

                // 2. Truy v?n DB l?y Payload v� Ch? k�
                // Quan tr?ng: Ph?i l?y c?t Payload, v� d� l� d? li?u g?c l�c k�
                var hoaDon = await _dbContext.HoaDons
                    .AsNoTracking() // Kh�ng c?n track changes
                    .FirstOrDefaultAsync(h => h.MaHoaDon == maHoaDon);

                if (hoaDon == null)
                {
                    return Json(new { success = false, message = $"Kh�ng t�m th?y h�a don #{maHoaDon} tr�n h? th?ng." });
                }

                if (string.IsNullOrEmpty(hoaDon.Payload) || string.IsNullOrEmpty(hoaDon.ChuKySo))
                {
                    return Json(new { success = false, message = "H�a don n�y chua du?c k� s? ho?c thi?u d? li?u g?c." });
                }

                // 3. G?i RSA Service d? ki?m tra
                // Tham s? 1: Payload (JSON chu?i) l?y t? DB
                // Tham s? 2: Ch? k� (Base64) l?y t? DB
                bool isValid = _rsaService.Verify(hoaDon.Payload, hoaDon.ChuKySo);

                // 4. Tr? k?t qu?
                if (isValid)
                {
                    return Json(new
                    {
                        success = true,
                        isValid = true,
                        maHoaDon = hoaDon.MaHoaDon,
                        ngayXuat = hoaDon.NgayXuat?.ToString("dd/MM/yyyy HH:mm"),
                        trangThai = hoaDon.TrangThai,
                        message = "H�a don H?P L?. Ch? k� s? kh?p ho�n to�n v?i d? li?u g?c."
                    });
                }
                else
                {
                    return Json(new
                    {
                        success = true,
                        isValid = false,
                        maHoaDon = hoaDon.MaHoaDon,
                        message = "C?NH B�O: Ch? k� s? KH�NG KH?P! D? li?u c� th? d� b? s?a d?i."
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Loi he thong: " + ex.Message });
            }
        }
    }
}












