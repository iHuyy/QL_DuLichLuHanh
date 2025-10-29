using DuLich.Models;
using DuLich.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DuLich.Controllers
{
    [Authorize(Roles = "ROLE_ADMIN")]
    public class AdminController : Controller
    {
        private readonly OracleAuthService _authService;
        private readonly DuLich.Models.Data.ApplicationDbContext _db;

        public AdminController(OracleAuthService authService, DuLich.Models.Data.ApplicationDbContext db)
        {
            _authService = authService;
            _db = db;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login(LoginModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var (success, role) = await _authService.ValidateLoginAsync(model.Username, model.Password);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, "Đăng nhập không thành công");
                return View(model);
            }

            // Accept admin or staff roles here
            if (role == "ROLE_ADMIN" || role == "ROLE_STAFF" || role == "ROLE_CUSTOMER")
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, model.Username),
                    new Claim(ClaimTypes.Role, role)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties();

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                if (role == "ROLE_ADMIN")
                    return RedirectToAction("Dashboard", "Admin");

                if (role == "ROLE_STAFF")
                    return RedirectToAction("Index", "Staff");

                // default fallback for customers (if they reach this login)
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, "Đăng nhập không thành công");
            return View(model);
        }

        [HttpGet]
        public IActionResult Dashboard()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // --- Admin management actions (from outer AdminController) ---
        public async Task<IActionResult> Index()
        {
            return View();
        }

        public async Task<IActionResult> Users()
        {
            // Load users from KhachHang table for the management UI
            var users = await _db.KhachHangs.OrderBy(u => u.MaKhachHang).ToListAsync();
            return View(users);
        }

        // List only staff (NhanVien)
        public async Task<IActionResult> Staffs()
        {
            var staffs = await _db.NhanViens.OrderBy(u => u.MaNhanVien).ToListAsync();
            return View(staffs);
        }

        [HttpGet]
        public IActionResult CreateUser(string? role)
        {
            var model = new RegisterModel();
            if (!string.IsNullOrEmpty(role)) model.Role = role;
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser(RegisterModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var role = model.Role ?? "KhachHang";

            (bool success, string? message) result;

            if (role == "NhanVien")
            {
                result = await _authService.RegisterStaffAsync(model.Username, model.Password, model.HoTen, model.Email, model.SoDienThoai, model.ChiNhanh);
            }
            else if (role == "Admin")
            {
                result = await _authService.RegisterAdminAsync(model.Username, model.Password, model.HoTen, model.Email, model.SoDienThoai, model.DiaChi);
            }
            else
            {
                result = await _authService.RegisterCustomerAsync(model.Username, model.Password, model.HoTen, model.Email, model.SoDienThoai, model.DiaChi);
            }

            if (result.success)
            {
                TempData["Success"] = result.message;
                // Redirect to staff list when creating staff
                if (role == "NhanVien") return RedirectToAction("Staffs");
                return RedirectToAction("Users");
            }

            ModelState.AddModelError(string.Empty, result.message ?? "Không thể tạo người dùng");
            return View(model);
        }

        public async Task<IActionResult> Details(string username)
        {
            // Try to find by ORACLE_USERNAME first, otherwise lookup in Oracle
            var kh = await _db.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME == username.ToUpper() || k.ORACLE_USERNAME == username);
            if (kh != null)
            {
                return View(kh);
            }

            var user = await _authService.GetUserAsync(username);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string username)
        {
            var (success, message) = await _authService.DeleteUserAsync(username);
            TempData[success ? "Success" : "Error"] = message;
            return RedirectToAction("Users");
        }

        [HttpPost]
        public async Task<IActionResult> GrantRole(string username, string role)
        {
            var (success, message) = await _authService.GrantRoleAsync(username, role);
            TempData[success ? "Success" : "Error"] = message;
            return RedirectToAction("Details", new { username });
        }

        // --- KhachHang edit endpoints ---
        [HttpGet]
        public async Task<IActionResult> EditUser(int id)
        {
            var kh = await _db.KhachHangs.FindAsync(id);
            if (kh == null) return NotFound();
            return View(kh);
        }

        [HttpPost]
        public async Task<IActionResult> EditUser(DuLich.Models.KhachHang model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var kh = await _db.KhachHangs.FindAsync(model.MaKhachHang);
            if (kh == null) return NotFound();

            kh.HoTen = model.HoTen;
            kh.Email = model.Email;
            kh.SoDienThoai = model.SoDienThoai;
            kh.DiaChi = model.DiaChi;
            kh.VaiTro = model.VaiTro;
            kh.TrangThai = model.TrangThai;
            kh.ORACLE_USERNAME = model.ORACLE_USERNAME;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Cập nhật thông tin người dùng thành công";
            return RedirectToAction("Users");
        }

        [HttpPost]
        public async Task<IActionResult> ChangeUserRole(int id, string vaiTro)
        {
            var kh = await _db.KhachHangs.FindAsync(id);
            if (kh == null) return NotFound();

            kh.VaiTro = vaiTro;
            await _db.SaveChangesAsync();

            // If ORACLE_USERNAME exists and role corresponds to DB roles, propagate to Oracle via GrantRole
            if (!string.IsNullOrEmpty(kh.ORACLE_USERNAME))
            {
                var oracleRole = vaiTro == "Admin" ? "ROLE_ADMIN" : (vaiTro == "NhanVien" ? "ROLE_STAFF" : "ROLE_CUSTOMER");
                await _authService.GrantRoleAsync(kh.ORACLE_USERNAME!, oracleRole);
            }

            TempData["Success"] = "Đã thay đổi vai trò người dùng";
            return RedirectToAction("Users");
        }

        // --- NhanVien (staff) management ---
        [HttpGet]
        public async Task<IActionResult> EditStaff(int id)
        {
            var nv = await _db.NhanViens.FindAsync(id);
            if (nv == null) return NotFound();
            return View(nv);
        }

        [HttpPost]
        public async Task<IActionResult> EditStaff(DuLich.Models.NhanVien model)
        {
            if (!ModelState.IsValid) return View(model);
            var nv = await _db.NhanViens.FindAsync(model.MaNhanVien);
            if (nv == null) return NotFound();
            nv.HoTen = model.HoTen;
            nv.Email = model.Email;
            nv.SoDienThoai = model.SoDienThoai;
            nv.VaiTro = model.VaiTro;
            nv.TrangThai = model.TrangThai;
            nv.ORACLE_USERNAME = model.ORACLE_USERNAME;
            nv.ChiNhanh = model.ChiNhanh;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Cập nhật nhân viên thành công";
            return RedirectToAction("Staffs");
        }

        [HttpPost]
        public async Task<IActionResult> ChangeStaffRole(int id, string vaiTro)
        {
            var nv = await _db.NhanViens.FindAsync(id);
            if (nv == null) return NotFound();
            nv.VaiTro = vaiTro;
            await _db.SaveChangesAsync();
            if (!string.IsNullOrEmpty(nv.ORACLE_USERNAME))
            {
                var oracleRole = vaiTro == "Admin" ? "ROLE_ADMIN" : (vaiTro == "NhanVien" ? "ROLE_STAFF" : "ROLE_CUSTOMER");
                await _authService.GrantRoleAsync(nv.ORACLE_USERNAME!, oracleRole);
            }
            TempData["Success"] = "Đã thay đổi vai trò nhân viên";
            return RedirectToAction("Staffs");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteStaff(string username)
        {
            // delete NHANVIEN record then drop oracle user
            var nv = await _db.NhanViens.FirstOrDefaultAsync(n => n.ORACLE_USERNAME == username.ToUpper() || n.ORACLE_USERNAME == username);
            if (nv != null)
            {
                _db.NhanViens.Remove(nv);
                await _db.SaveChangesAsync();
            }
            var (success, message) = await _authService.DeleteUserAsync(username);
            TempData[success ? "Success" : "Error"] = message;
            return RedirectToAction("Staffs");
        }

        // --- Tour management ---
        public async Task<IActionResult> Tours()
        {
            var tours = await _db.Tours.OrderBy(t => t.MaTour).ToListAsync();
            return View(tours);
        }

        [HttpGet]
        public IActionResult CreateTour()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateTour(Tour model)
        {
            if (!ModelState.IsValid) return View(model);
            _db.Tours.Add(model);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Tạo tour thành công";
            return RedirectToAction("Tours");
        }

        [HttpGet]
        public async Task<IActionResult> EditTour(int id)
        {
            var t = await _db.Tours.FindAsync(id);
            if (t == null) return NotFound();
            return View(t);
        }

        [HttpPost]
        public async Task<IActionResult> EditTour(Tour model)
        {
            if (!ModelState.IsValid) return View(model);
            var t = await _db.Tours.FindAsync(model.MaTour);
            if (t == null) return NotFound();
            t.TieuDe = model.TieuDe;
            t.MoTa = model.MoTa;
            t.NoiKhoiHanh = model.NoiKhoiHanh;
            t.NoiDen = model.NoiDen;
            t.ThanhPho = model.ThanhPho;
            t.ThoiGian = model.ThoiGian;
            t.GiaNguoiLon = model.GiaNguoiLon;
            t.GiaTreEm = model.GiaTreEm;
            t.TrangThai = model.TrangThai;
            t.SoLuong = model.SoLuong;
            t.QR = model.QR;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Cập nhật tour thành công";
            return RedirectToAction("Tours");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTour(int id)
        {
            var t = await _db.Tours.FindAsync(id);
            if (t == null) return NotFound();
            _db.Tours.Remove(t);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Xóa tour thành công";
            return RedirectToAction("Tours");
        }

        // Manage images for a tour
        public async Task<IActionResult> ManageImages(int tourId)
        {
            var images = await _db.AnhTours.Where(a => a.MaTour == tourId).ToListAsync();
            ViewBag.TourId = tourId;
            return View(images);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteImage(int id, int tourId)
        {
            var img = await _db.AnhTours.FindAsync(id);
            if (img == null) return NotFound();
            _db.AnhTours.Remove(img);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Xóa ảnh thành công";
            return RedirectToAction("ManageImages", new { tourId });
        }
    }
}