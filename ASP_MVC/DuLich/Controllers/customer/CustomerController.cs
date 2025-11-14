using DuLich.Models;
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
        private readonly DigitalSignatureService _digitalSignatureService;
    private readonly ApplicationDbContext _dbContext;

        public CustomerController(OracleAuthService authService, ApplicationDbContext context, DigitalSignatureService digitalSignatureService) : base(context)
        {
            _authService = authService;
            _digitalSignatureService = digitalSignatureService;
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

            ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng");
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
                var images = await _context.AnhTours
                    .Where(a => a.MaTour == t.MaTour)
                    .OrderBy(a => a.MaAnh)
                    .Select(a => a.DuongDanAnh)
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
                    Images = images.Where(s => !string.IsNullOrEmpty(s)).Select(s => s!).ToList(),
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
            return RedirectToAction("Index", "Home");
        }

        private static string GenerateSessionId()
        {
            // Use GUID without dashes for compactness
            return Guid.NewGuid().ToString("N");
        }

        [HttpGet]
        [Authorize(Roles = "ROLE_CUSTOMER")]
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

                var images = await _context.AnhTours
                    .Where(a => a.MaTour == tour.MaTour)
                    .OrderBy(a => a.MaAnh)
                    .Select(a => a.DuongDanAnh)
                    .ToListAsync();

                var rating = await _context.DanhGiaTours
                    .Where(d => d.MaTour == tour.MaTour)
                    .AverageAsync(d => (decimal?)d.SoSao) ?? 0;

                string bookingStatusChar = "b"; // Default to pending
                if (booking.TrangThaiDat == "Đã xác nhận" && tour.ThoiGian > DateTime.Now)
                {
                    bookingStatusChar = "y"; // Upcoming
                }
                else if (booking.TrangThaiDat == "Đã xác nhận" && tour.ThoiGian <= DateTime.Now)
                {
                    bookingStatusChar = "f"; // Finished
                }
                else if (booking.TrangThaiDat == "Đã hủy")
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
                    Images = images.Where(s => !string.IsNullOrEmpty(s)).Select(s => s!).ToList(),
                    Rating = rating,
                    IsPaid = booking.HoaDon?.TrangThai == "Đã thanh toán"
                });
            }

            // Fetch popular tours (similar logic to Index action)
            var popularToursQuery = _context.Tours.AsQueryable();
            var popularTours = await popularToursQuery.OrderBy(t => t.MaTour).Take(4).ToListAsync();

            foreach (var t in popularTours)
            {
                var images = await _context.AnhTours
                    .Where(a => a.MaTour == t.MaTour)
                    .OrderBy(a => a.MaAnh)
                    .Select(a => a.DuongDanAnh)
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
                    Images = images.Where(s => !string.IsNullOrEmpty(s)).Select(s => s!).ToList(),
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
                TenTour = tour.TieuDe ?? "Chưa có tên",
                MoTa = tour.MoTa,
                DiemKhoiHanh = tour.NoiKhoiHanh ?? "Chưa xác định",
                DiemDen = tour.NoiDen ?? tour.ThanhPho ?? "Chưa xác định",
                NgayKhoiHanh = tour.ThoiGian ?? DateTime.Now,
                NgayKetThuc = tour.ThoiGian?.AddDays(5) ?? DateTime.Now.AddDays(5), // Giả sử tour kéo dài 5 ngày
                Gia = tour.GiaNguoiLon ?? 0,
                SoLuong = tour.SoLuong ?? 0
            };

            ViewBag.Images = await _context.AnhTours
                .Where(a => a.MaTour == id)
                .OrderBy(a => a.MaAnh)
                .Select(a => a.DuongDanAnh)
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(s => s!)
                .ToListAsync();

            ViewBag.Rating = await _context.DanhGiaTours
                .Where(d => d.MaTour == id)
                .AverageAsync(d => (decimal?)d.SoSao) ?? 0;

            ViewBag.RelatedTours = await _context.Tours
                .Where(t => t.MaTour != id && (t.NoiDen == model.DiemDen || t.ThanhPho == model.DiemDen))
                .OrderBy(t => t.MaTour)
                .Take(3)
                .Select(t => new TourDetailViewModel {
                    MaTour = t.MaTour,
                    TenTour = t.TieuDe ?? "Chưa có tên",
                    Gia = t.GiaNguoiLon ?? 0
                })
                .ToListAsync();

            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = "ROLE_CUSTOMER")]
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
        [Authorize(Roles = "ROLE_CUSTOMER")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Booking([FromForm] CreateBookingViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var tour = await _context.Tours.FindAsync(model.TourId);
            if (tour == null)
            {
                return NotFound();
            }

            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized();
            }

            var customer = await _context.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME.ToUpper() == username.ToUpper());
            if (customer == null)
            {
                return Unauthorized();
            }

            var totalQuantity = model.NumAdults + model.NumChildren;
            if (tour.SoLuong.HasValue && totalQuantity > tour.SoLuong.Value)
            {
                ModelState.AddModelError(string.Empty, "Số lượng người đặt vượt quá số chỗ còn trống");
                return View(model);
            }

            var booking = new DatTour
            {
                MaTour = model.TourId,
                MaKhachHang = customer.MaKhachHang,
                NgayDat = DateTime.Now,
                SoNguoiLon = model.NumAdults,
                SoTreEm = model.NumChildren,
                TongTien = (model.NumAdults * (tour.GiaNguoiLon ?? 0)) + (model.NumChildren * (tour.GiaTreEm ?? 0)),
                TrangThaiDat = "Chờ xác nhận",
                TrangThaiThanhToan = "Chưa thanh toán",
                YeuCauDacBiet = model.SpecialRequest
            };

            _context.DatTours.Add(booking);
            await _context.SaveChangesAsync();

            // Create invoice
            var hoaDon = new HoaDon
            {
                MaDatTour = booking.MaDatTour,
                NgayXuat = DateTime.Now,
                SoTien = booking.TongTien,
                TrangThai = "Chưa thanh toán"
            };

            // Generate signature data for the invoice
            var signatureData = $"{booking.MaDatTour}|{booking.MaKhachHang}|{booking.MaTour}|{booking.TongTien}|{hoaDon.NgayXuat:yyyy-MM-dd HH:mm:ss}";
            hoaDon.ChuKySo = _digitalSignatureService.SignData(signatureData);

            _context.HoaDons.Add(hoaDon);
            await _context.SaveChangesAsync();

            return RedirectToAction("MyTour");
        }

        [HttpGet]
        [Authorize(Roles = "ROLE_CUSTOMER")]
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
        [Authorize(Roles = "ROLE_CUSTOMER")]
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
                BookingStatus = booking.TrangThaiDat == "Đã xác nhận" && tour.ThoiGian > DateTime.Now ? "y" :
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
        [Authorize(Roles = "ROLE_CUSTOMER")]
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
            if (booking.TrangThaiDat != "Đã hủy" && booking.TrangThaiDat != "Đã hoàn thành")
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
        [Authorize(Roles = "ROLE_CUSTOMER")]
        public async Task<IActionResult> Payment(int bookingId)
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
                .Include(b => b.Tour)
                .Include(b => b.HoaDon)
                .FirstOrDefaultAsync(b => b.MaDatTour == bookingId && b.MaKhachHang == customer.MaKhachHang);

            if (booking == null || booking.HoaDon == null || booking.Tour == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đặt tour hoặc hóa đơn";
                return RedirectToAction("MyTour");
            }

            // Tạo dữ liệu để xác thực chữ ký số
            var signatureData = $"{booking.MaDatTour}|{booking.MaKhachHang}|{booking.MaTour}|{booking.TongTien}|{booking.HoaDon.NgayXuat:yyyy-MM-dd HH:mm:ss}";
            
            // Đọc public key
            var publicKeyPath = Path.Combine(Directory.GetCurrentDirectory(), "Keys", "public_key.pem");
            var publicKeyPem = await System.IO.File.ReadAllTextAsync(publicKeyPath);
            
            // Xác thực chữ ký số
            bool isValid = _digitalSignatureService.VerifySignature(
                signatureData,
                booking.HoaDon.ChuKySo ?? "",
                publicKeyPem
            );

            var model = new InvoiceViewModel
            {
                MaHoaDon = booking.HoaDon.MaHoaDon,
                NgayXuat = booking.HoaDon.NgayXuat,
                SoTien = booking.HoaDon.SoTien,
                TrangThai = booking.HoaDon.TrangThai,
                IsSignatureValid = isValid,

                // Thông tin tour
                TenTour = booking.Tour.TieuDe,
                NgayKhoiHanh = booking.Tour.ThoiGian,
                SoNguoiLon = booking.SoNguoiLon,
                SoTreEm = booking.SoTreEm,

                // Thông tin khách hàng
                TenKhachHang = customer.HoTen,
                Email = customer.Email,
                SoDienThoai = customer.SoDienThoai,
                DiaChi = customer.DiaChi
            };

            // expose booking id to view so the payment form can post it
            ViewBag.BookingId = booking.MaDatTour;

            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "ROLE_CUSTOMER")]
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
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đặt tour hoặc hóa đơn";
                return RedirectToAction("MyTour");
            }

            if (booking.HoaDon.TrangThai != "Đã thanh toán")
            {
                booking.HoaDon.TrangThai = "Đã thanh toán";
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
                    var signerName = signer?.HoTen ?? "Người quản lý";

                    var pdfBytes = CreateInvoicePdf(hoaDon, booking, booking.Tour, await _context.KhachHangs.FirstOrDefaultAsync(k => k.MaKhachHang == booking.MaKhachHang), signerName);

                    var invoicesDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "invoices");
                    Directory.CreateDirectory(invoicesDir);
                    var filePath = Path.Combine(invoicesDir, $"HoaDon_{hoaDon.MaHoaDon}.pdf");
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

            return RedirectToAction("TourBooked", new { bookingId = bookingId });
        }

        [HttpGet]
        [Authorize(Roles = "ROLE_CUSTOMER")]
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
        [Authorize(Roles = "ROLE_CUSTOMER")]
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
                    Paragraph title = new Paragraph("HÓA ĐƠN ĐẶT TOUR", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18));
                    title.Alignment = Element.ALIGN_CENTER;
                    document.Add(title);
                    document.Add(new Paragraph(" "));

                    // Invoice header info
                    PdfPTable headerTable = new PdfPTable(2);
                    headerTable.WidthPercentage = 100;
                    headerTable.AddCell("Mã hóa đơn: " + hoaDon.MaHoaDon);
                    headerTable.AddCell("Ngày xuất: " + (hoaDon.NgayXuat?.ToString("dd/MM/yyyy HH:mm:ss") ?? ""));
                    headerTable.AddCell("Trạng thái: " + hoaDon.TrangThai);
                    headerTable.AddCell(" ");
                    document.Add(headerTable);
                    document.Add(new Paragraph(" "));

                    // Customer info section
                    document.Add(new Paragraph("THÔNG TIN KHÁCH HÀNG", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12)));
                    PdfPTable customerTable = new PdfPTable(2);
                    customerTable.WidthPercentage = 100;
                    customerTable.AddCell("Tên: " + (customer?.HoTen ?? ""));
                    customerTable.AddCell("Email: " + (customer?.Email ?? ""));
                    customerTable.AddCell("Điện thoại: " + (customer?.SoDienThoai ?? ""));
                    customerTable.AddCell("Địa chỉ: " + (customer?.DiaChi ?? ""));
                    document.Add(customerTable);
                    document.Add(new Paragraph(" "));

                    // Tour details
                    document.Add(new Paragraph("CHI TIẾT TOUR", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12)));
                    PdfPTable tourTable = new PdfPTable(2);
                    tourTable.WidthPercentage = 100;
                    tourTable.AddCell("Tên tour: " + (tour?.TieuDe ?? ""));
                    tourTable.AddCell("Ngày khởi hành: " + (tour?.ThoiGian?.ToString("dd/MM/yyyy") ?? ""));
                    tourTable.AddCell("Số người lớn: " + (booking?.SoNguoiLon ?? 0));
                    tourTable.AddCell("Số trẻ em: " + (booking?.SoTreEm ?? 0));
                    document.Add(tourTable);
                    document.Add(new Paragraph(" "));

                    // Total
                    Paragraph total = new Paragraph($"TỔNG TIỀN: {(hoaDon.SoTien ?? 0):N0} VNĐ", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14));
                    total.Alignment = Element.ALIGN_CENTER;
                    document.Add(total);
                    document.Add(new Paragraph(" "));

                    // Signature info
                    var signatureData = $"{booking?.MaDatTour}|{booking?.MaKhachHang}|{booking?.MaTour}|{booking?.TongTien}|{(hoaDon.NgayXuat?.ToString("yyyy-MM-dd HH:mm:ss") ?? "")}";
                    byte[] hashBytes;
                    try
                    {
                        hashBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(signatureData));
                    }
                    catch
                    {
                        hashBytes = new byte[0];
                    }
                    var hashHex = hashBytes.Length > 0 ? BitConverter.ToString(hashBytes).Replace("-", "") : string.Empty;
                    var authCode = hashHex.Length >= 12 ? hashHex.Substring(0, 12) : hashHex;

                    document.Add(new Paragraph("THÔNG TIN XÁC THỰC", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12)));
                    PdfPTable signTable = new PdfPTable(2);
                    signTable.WidthPercentage = 100;
                    signTable.AddCell("Chữ ký số:\n" + (hoaDon.ChuKySo ?? ""));
                    signTable.AddCell("Mã xác thực: " + authCode);
                    document.Add(signTable);
                    document.Add(new Paragraph(" "));
                    document.Add(new Paragraph("Hash (SHA256): " + hashHex, FontFactory.GetFont(FontFactory.HELVETICA, 9)));
                    document.Add(new Paragraph(" "));

                    // Signature lines
                    document.Add(new Paragraph("KỲ DỮA", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12)));
                    PdfPTable signatureTable = new PdfPTable(2);
                    signatureTable.WidthPercentage = 100;
                    PdfPCell cell1 = new PdfPCell(new Phrase("Người lập\n\n\n\n(Ký và ghi rõ họ tên)"));
                    cell1.MinimumHeight = 80;
                    PdfPCell cell2 = new PdfPCell(new Phrase($"Người ký: {signerName}\n\n\n"));
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

        private string GenerateInvoiceHtml(HoaDon hoaDon, DatTour? booking, Tour? tour, KhachHang? customer, string signerName)
        {
            var signatureData = $"{booking?.MaDatTour}|{booking?.MaKhachHang}|{booking?.MaTour}|{booking?.TongTien}|{(hoaDon.NgayXuat?.ToString("yyyy-MM-dd HH:mm:ss") ?? "")}";
            byte[] hashBytes;
            try
            {
                hashBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(signatureData));
            }
            catch
            {
                hashBytes = new byte[0];
            }
            var hashHex = hashBytes.Length > 0 ? BitConverter.ToString(hashBytes).Replace("-", "") : string.Empty;
            var authCode = hashHex.Length >= 12 ? hashHex.Substring(0, 12) : hashHex;
            var total = hoaDon.SoTien ?? 0m;

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .header {{ text-align: center; margin-bottom: 30px; }}
        .header h1 {{ font-size: 24px; margin: 0; }}
        table {{ width: 100%; border-collapse: collapse; margin-bottom: 20px; }}
        td {{ padding: 8px; border: 1px solid #ddd; }}
        .label {{ font-weight: bold; }}
        .total {{ font-weight: bold; font-size: 14px; }}
        @media print {{ body {{ margin: 0; }} }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>HÓA ĐƠN ĐẶT TOUR</h1>
    </div>
    
    <table>
        <tr><td class='label'>Mã hóa đơn:</td><td>{hoaDon.MaHoaDon}</td></tr>
        <tr><td class='label'>Ngày xuất:</td><td>{hoaDon.NgayXuat?.ToString("dd/MM/yyyy HH:mm:ss")}</td></tr>
        <tr><td class='label'>Trạng thái:</td><td>{hoaDon.TrangThai}</td></tr>
    </table>
    
    <h3>Khách hàng</h3>
    <table>
        <tr><td class='label'>Tên:</td><td>{customer?.HoTen}</td></tr>
        <tr><td class='label'>Email:</td><td>{customer?.Email}</td></tr>
        <tr><td class='label'>Điện thoại:</td><td>{customer?.SoDienThoai}</td></tr>
        <tr><td class='label'>Địa chỉ:</td><td>{customer?.DiaChi}</td></tr>
    </table>
    
    <h3>Chi tiết tour</h3>
    <table>
        <tr><td class='label'>Tên tour:</td><td>{tour?.TieuDe}</td></tr>
        <tr><td class='label'>Ngày khởi hành:</td><td>{tour?.ThoiGian?.ToString("dd/MM/yyyy")}</td></tr>
        <tr><td class='label'>Số người lớn:</td><td>{booking?.SoNguoiLon}</td></tr>
        <tr><td class='label'>Số trẻ em:</td><td>{booking?.SoTreEm}</td></tr>
    </table>
    
    <h3 class='total'>Tổng tiền: {total:N0} VNĐ</h3>
    
    <h3>Thông tin xác thực</h3>
    <table>
        <tr><td class='label'>Chữ ký số:</td><td>{hoaDon.ChuKySo}</td></tr>
        <tr><td class='label'>Hash (SHA256):</td><td style='word-break: break-all;'>{hashHex}</td></tr>
        <tr><td class='label'>Mã xác thực:</td><td>{authCode}</td></tr>
    </table>
    
    <h3>Ký duyệt</h3>
    <table>
        <tr><td style='text-align: center; padding: 40px;'>Người lập<br/><br/><br/>(Ký và ghi rõ họ tên)</td><td style='text-align: center; padding: 40px;'>Người ký: {signerName}<br/><br/><br/></td></tr>
    </table>
    
    <script>
        // Auto-print on load (optional)
        // window.print();
    </script>
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
    }
}