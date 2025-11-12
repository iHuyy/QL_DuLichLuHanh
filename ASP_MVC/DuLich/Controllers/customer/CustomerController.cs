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
                            UserType = "KhachHang",
                            DeviceType = "WEB",
                            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                            DeviceInfo = Request.Headers["User-Agent"].ToString(),
                            IsActive = "Y",
                            LoginTime = DateTime.UtcNow,
                            LastActivity = DateTime.UtcNow
                        };

                               // Remove any previous sessions for this user (global) to enforce single active session and avoid table growth
                               var prev = _dbContext.UserSessions.Where(s => s.UserId == customer.MaKhachHang && s.UserType == "KhachHang").ToList();
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
                TempData["SuccessMessage"] = "Thanh toán thành công!";
            }

            return RedirectToAction("TourBooked", new { bookingId = bookingId });
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