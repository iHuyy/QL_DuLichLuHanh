using DuLich.Models;
using DuLich.Services;
using DuLich.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DuLich.Controllers
{
    public class CustomerController : Controller
    {
        private readonly OracleAuthService _authService;
        private readonly ApplicationDbContext _db;

        public CustomerController(OracleAuthService authService, ApplicationDbContext db)
        {
            _authService = authService;
            _db = db;
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
                    new Claim(ClaimTypes.Role, "Customer")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties();

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                return RedirectToAction("Index", "Home");
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
            // Load tours and apply optional search filters from the banner form
            var q = _db.Tours.AsQueryable();

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
                // ensure from <= to
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
            var model = new DuLich.Models.CustomerHomeViewModel();

            foreach (var t in tours)
            {
                var images = await _db.AnhTours
                    .Where(a => a.MaTour == t.MaTour)
                    .OrderBy(a => a.MaAnh)
                    .Select(a => a.DuongDanAnh)
                    .ToListAsync();

                model.Tours.Add(new DuLich.Models.TourItem
                {
                    MaTour = t.MaTour,
                    Title = t.TieuDe ?? string.Empty,
                    Destination = t.NoiDen ?? t.NoiKhoiHanh ?? t.ThanhPho ?? string.Empty,
                    Time = t.ThoiGian?.ToString("yyyy-MM-dd") ?? string.Empty,
                    PriceAdult = t.GiaNguoiLon ?? 0,
                    Images = images.Where(s => !string.IsNullOrEmpty(s)).Select(s => s!).ToList()
                });
            }

            // For now use the same set as popular
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
    }
}