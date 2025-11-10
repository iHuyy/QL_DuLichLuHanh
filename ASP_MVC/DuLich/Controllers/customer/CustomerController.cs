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

        public CustomerController(OracleAuthService authService, ApplicationDbContext context, DigitalSignatureService digitalSignatureService) : base(context)
        {
            _authService = authService;
            _digitalSignatureService = digitalSignatureService;
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
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
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
            var username = User.Identity.Name;
            var customer = await _context.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME.ToUpper() == username.ToUpper());

            if (customer == null)
            {
                return RedirectToAction("Login");
            }

            var booking = await _context.DatTours
                .Where(dt => dt.MaDatTour == bookingId && dt.MaKhachHang == customer.MaKhachHang)
                .Include(dt => dt.HoaDon)
                .FirstOrDefaultAsync();

            if (booking == null || booking.HoaDon == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đặt tour hoặc hóa đơn.";
                return RedirectToAction("MyTour");
            }

            if (booking.HoaDon.TrangThai != "Đã thanh toán")
            {
                booking.HoaDon.TrangThai = "Đã thanh toán";
                _context.HoaDons.Update(booking.HoaDon);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Thanh toán thành công!";
            }
            else
            {
                TempData["InfoMessage"] = "Tour này đã được thanh toán.";
            }

            return RedirectToAction("TourBooked", new { bookingId = bookingId });
        }
    }
}