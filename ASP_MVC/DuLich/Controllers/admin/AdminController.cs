using DuLich.Models;
using DuLich.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Rendering;
using DuLich.Models.Data;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace DuLich.Controllers
{
    [Authorize(Roles = "ROLE_ADMIN")]
    public class AdminController : BaseController
    {
        private readonly OracleAuthService _authService;
        private readonly IWebHostEnvironment _env;

        public AdminController(OracleAuthService authService, ApplicationDbContext context, IWebHostEnvironment env) : base(context)
        {
            _authService = authService;
            _env = env;
        }

        [AllowAnonymous]
        [HttpGet]
        [Route("Admin/Login")]
        public IActionResult Login()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [Route("Admin/Login")]
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

            if (role == "ROLE_ADMIN" || role == "ROLE_STAFF" || role == "ROLE_CUSTOMER")
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, model.Username),
                    new Claim(ClaimTypes.Role, role)
                };

                if (role == "ROLE_STAFF")
                {
                    var staff = await _context.NhanViens.FirstOrDefaultAsync(n => n.ORACLE_USERNAME == model.Username.ToUpper());
                    if (staff != null && staff.MaChiNhanh.HasValue)
                    {
                        claims.Add(new Claim("MaChiNhanh", staff.MaChiNhanh.Value.ToString()));
                    }
                }

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

                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, "Đăng nhập không thành công");
            return View(model);
        }

        [HttpGet]
        [Route("Admin/Dashboard")]
        public IActionResult Dashboard()
        {
            return View();
        }

        [HttpPost]
        [Route("Admin/Logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var username = User?.Identity?.Name;
                if (!string.IsNullOrEmpty(username))
                {
                    int userId = -1;
                    var kh = await _context.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME != null && (k.ORACLE_USERNAME.ToUpper() == username.ToUpper() || k.ORACLE_USERNAME == username));
                    if (kh != null) userId = kh.MaKhachHang;
                    else
                    {
                        var nv = await _context.NhanViens.FirstOrDefaultAsync(n => n.ORACLE_USERNAME != null && (n.ORACLE_USERNAME.ToUpper() == username.ToUpper() || n.ORACLE_USERNAME == username));
                        if (nv != null) userId = nv.MaNhanVien;
                    }

                    if (userId != -1)
                    {
                        var sessions = _context.UserSessions.Where(s => s.UserId == userId).ToList();
                        if (sessions.Any())
                        {
                            _context.UserSessions.RemoveRange(sessions);
                            await _context.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        // fallback cookie removal
                        var sessionId = Request.Cookies["USER_SESSION_ID"];
                        if (!string.IsNullOrEmpty(sessionId))
                        {
                            var sess = await _context.UserSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
                            if (sess != null)
                            {
                                _context.UserSessions.Remove(sess);
                                await _context.SaveChangesAsync();
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // ignore cleanup errors during logout
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [Route("Admin")]
        public IActionResult Index()
        {
            return View();
        }

        [Route("admin/customer")]
        public async Task<IActionResult> Users()
        {
            var users = await _context.KhachHangs.OrderBy(u => u.MaKhachHang).ToListAsync();
            return View(users);
        }

        [Route("admin/staff")]
        public async Task<IActionResult> Staffs()
        {
            var staffs = await _context.NhanViens.Include(n => n.ChiNhanh).OrderBy(u => u.MaNhanVien).ToListAsync();
            ViewBag.ChiNhanhs = await _context.ChiNhanhs.ToListAsync();
            ViewBag.Roles = new List<string> { "Admin", "NhanVien" };
            return View(staffs);
        }

        [HttpGet]
        [Route("Admin/CreateUser")]
        public IActionResult CreateUser(string? role)
        {
            var chiNhanhs = _context.ChiNhanhs.ToList() ?? new List<ChiNhanh>();
            var model = new CreateUserViewModel
            {
                ChiNhanhSelectList = new SelectList(chiNhanhs, "MaChiNhanh", "TenChiNhanh")
            };

            if (!string.IsNullOrEmpty(role)) model.Role = role;
            return View(model);
        }

        [HttpPost]
        [Route("Admin/CreateUser")]
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var chiNhanhs = _context.ChiNhanhs.ToList() ?? new List<ChiNhanh>();
                model.ChiNhanhSelectList = new SelectList(chiNhanhs, "MaChiNhanh", "TenChiNhanh", model.MaChiNhanh);
                return View(model);
            }

            var role = model.Role ?? "KhachHang";

            (bool success, string? message) result;

            if (role == "NhanVien")
            {
                result = await _authService.RegisterStaffAsync(model.Username, model.Password, model.HoTen, model.Email, model.SoDienThoai, model.MaChiNhanh);
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
                if (role == "NhanVien") return RedirectToAction("Staffs");
                return RedirectToAction("Users");
            }

            ModelState.AddModelError(string.Empty, result.message ?? "Không thể tạo người dùng");
            var allChiNhanhs = _context.ChiNhanhs.ToList() ?? new List<ChiNhanh>();
            model.ChiNhanhSelectList = new SelectList(allChiNhanhs, "MaChiNhanh", "TenChiNhanh", model.MaChiNhanh);
            return View(model);
        }

        public async Task<IActionResult> Details(string username)
        {
            var kh = await _context.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME == username.ToUpper() || k.ORACLE_USERNAME == username);
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

        [HttpGet]
        public async Task<IActionResult> EditUser(int id)
        {
            var kh = await _context.KhachHangs.FindAsync(id);
            if (kh == null) return NotFound();
            return View(kh);
        }

        [HttpPost]
        public async Task<IActionResult> EditUser(KhachHang model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var kh = await _context.KhachHangs.FindAsync(model.MaKhachHang);
            if (kh == null) return NotFound();

            kh.HoTen = model.HoTen;
            kh.Email = model.Email;
            kh.SoDienThoai = model.SoDienThoai;
            kh.DiaChi = model.DiaChi;
            kh.VaiTro = model.VaiTro;
            kh.TrangThai = model.TrangThai;
            kh.ORACLE_USERNAME = model.ORACLE_USERNAME;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật thông tin người dùng thành công";
            return RedirectToAction("Users");
        }

        [HttpPost]
        public async Task<IActionResult> ChangeUserRole(int id, string vaiTro)
        {
            var kh = await _context.KhachHangs.FindAsync(id);
            if (kh == null) return NotFound();

            kh.VaiTro = vaiTro;
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(kh.ORACLE_USERNAME))
            {
                var oracleRole = vaiTro == "Admin" ? "ROLE_ADMIN" : (vaiTro == "NhanVien" ? "ROLE_STAFF" : "ROLE_CUSTOMER");
                await _authService.GrantRoleAsync(kh.ORACLE_USERNAME!, oracleRole);
            }

            TempData["Success"] = "Đã thay đổi vai trò người dùng";
            return RedirectToAction("Users");
        }

        [HttpPost]
        public async Task<IActionResult> ChangeUserStatus(int id, string trangThai)
        {
            var kh = await _context.KhachHangs.FindAsync(id);
            if (kh == null) return NotFound();

            kh.TrangThai = trangThai;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã thay đổi trạng thái người dùng";
            return RedirectToAction("Users");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUserById(int id)
        {
            var kh = await _context.KhachHangs.FindAsync(id);
            if (kh == null)
            {
                TempData["Error"] = "Không tìm thấy người dùng.";
                return RedirectToAction("Users");
            }

            if (!string.IsNullOrEmpty(kh.ORACLE_USERNAME))
            {
                var (success, message) = await _authService.DeleteUserAsync(kh.ORACLE_USERNAME);
                if (!success)
                {
                    TempData["Error"] = message;
                    return RedirectToAction("Users");
                }
            }
            
            _context.KhachHangs.Remove(kh);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa người dùng thành công.";
            return RedirectToAction("Users");
        }

        [HttpGet]
        public async Task<IActionResult> EditStaff(int id)
        {
            ViewBag.ChiNhanhs = await _context.ChiNhanhs.ToListAsync();
            var nv = await _context.NhanViens.FindAsync(id);
            if (nv == null) return NotFound();
            return View(nv);
        }

        [HttpPost]
        public async Task<IActionResult> EditStaff(NhanVien model)
        {
            if (!ModelState.IsValid) return View(model);
            var nv = await _context.NhanViens.FindAsync(model.MaNhanVien);
            if (nv == null) return NotFound();
            nv.HoTen = model.HoTen;
            nv.Email = model.Email;
            nv.SoDienThoai = model.SoDienThoai;
            nv.VaiTro = model.VaiTro;
            nv.TrangThai = model.TrangThai;
            nv.ORACLE_USERNAME = model.ORACLE_USERNAME;
            nv.MaChiNhanh = model.MaChiNhanh;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Cập nhật nhân viên thành công";
            return RedirectToAction("Staffs");
        }

        [HttpPost]
        public async Task<IActionResult> ChangeStaffRole(int id, string vaiTro)
        {
            var nv = await _context.NhanViens.FindAsync(id);
            if (nv == null) return NotFound();
            nv.VaiTro = vaiTro;
            await _context.SaveChangesAsync();
            if (!string.IsNullOrEmpty(nv.ORACLE_USERNAME))
            {
                var oracleRole = vaiTro == "Admin" ? "ROLE_ADMIN" : (vaiTro == "NhanVien" ? "ROLE_STAFF" : "ROLE_CUSTOMER");
                await _authService.GrantRoleAsync(nv.ORACLE_USERNAME!, oracleRole);
            }
            TempData["Success"] = "Đã thay đổi vai trò nhân viên";
            return RedirectToAction("Staffs");
        }

        [HttpPost]
        public async Task<IActionResult> ChangeStaffBranch(int id, int maChiNhanh)
        {
            var nv = await _context.NhanViens.FindAsync(id);
            if (nv == null) return NotFound();
            nv.MaChiNhanh = maChiNhanh;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã thay đổi chi nhánh nhân viên";
            return RedirectToAction("Staffs");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteStaff(string username)
        {
            var nv = await _context.NhanViens.FirstOrDefaultAsync(n => n.ORACLE_USERNAME == username.ToUpper() || n.ORACLE_USERNAME == username);
            if (nv != null)
            {
                _context.NhanViens.Remove(nv);
                await _context.SaveChangesAsync();
            }
            var (success, message) = await _authService.DeleteUserAsync(username);
            TempData[success ? "Success" : "Error"] = message;
            return RedirectToAction("Staffs");
        }

        public async Task<IActionResult> Tours()
        {
            var toursData = await _context.Tours
                .AsNoTracking()
                .Include(t => t.ChiNhanh)
                .OrderByDescending(t => t.MaTour)
                .Select(t => new
                {
                    Tour = t,
                    SoLuongDat = _context.DatTours
                        .Where(d => d.MaTour == t.MaTour && d.TrangThaiDat == "Đã xác nhận")
                        .Sum(d => (int?)(d.SoNguoiLon ?? 0) + (d.SoTreEm ?? 0)) ?? 0
                })
                .ToListAsync();

            var tourViewModels = toursData.Select(t_data => new TourViewModel
            {
                Id = t_data.Tour.MaTour,
                MaTour = t_data.Tour.MaTour.ToString(),
                TenTour = t_data.Tour.TieuDe ?? "Chưa đặt tên",
                DiemDen = t_data.Tour.NoiDen ?? "Chưa xác định",
                NgayKhoiHanh = t_data.Tour.ThoiGian ?? DateTime.Now,
                Gia = t_data.Tour.GiaNguoiLon ?? 0,
                SoLuong = t_data.Tour.SoLuong ?? 0,
                SoChoConLai = (t_data.Tour.SoLuong ?? 0) - t_data.SoLuongDat,
                TrangThai = t_data.Tour.TrangThai ?? "Chưa xác định",
                QR = t_data.Tour.QR ?? "",
                ChiNhanh = t_data.Tour.ChiNhanh?.TenChiNhanh,
                StatusClass = GetTourStatusClass(t_data.Tour.TrangThai ?? "")
            }).ToList();

            return View(tourViewModels);
        }

        [HttpGet]
        public async Task<IActionResult> TourDetails(int id)
        {
            var tour = await _context.Tours
               .Include(t => t.AnhTours)
               .Include(t => t.ChiNhanh)
               .AsNoTracking()
               .FirstOrDefaultAsync(m => m.MaTour == id);

            if (tour == null)
            {
                return NotFound();
            }

            var soLuongDat = await _context.DatTours
                .Where(d => d.MaTour == id && d.TrangThaiDat == "Đã xác nhận")
                .SumAsync(d => (int?)(d.SoNguoiLon ?? 0) + (d.SoTreEm ?? 0)) ?? 0;

            var tourViewModel = new TourViewModel
            {
                Id = tour.MaTour,
                MaTour = tour.MaTour.ToString(),
                TenTour = tour.TieuDe ?? "Chưa đặt tên",
                DiemDen = tour.NoiDen ?? "Chưa xác định",
                NoiKhoiHanh = tour.NoiKhoiHanh ?? "Chưa xác định",
                NgayKhoiHanh = tour.ThoiGian ?? DateTime.Now,
                Gia = tour.GiaNguoiLon ?? 0,
                GiaTreEm = tour.GiaTreEm,
                SoLuong = tour.SoLuong ?? 0,
                SoChoConLai = (tour.SoLuong ?? 0) - soLuongDat,
                TrangThai = tour.TrangThai ?? "Chưa xác định",
                MoTa = tour.MoTa ?? "",
                QR = tour.QR ?? "",
                ChiNhanh = tour.ChiNhanh?.TenChiNhanh ?? "",
                StatusClass = GetTourStatusClass(tour.TrangThai ?? ""),
                AnhTours = tour.AnhTours
            };

            return View(tourViewModel);
        }

        [HttpGet]
        public async Task<IActionResult> EditTour(int id)
        {
            var t = await _context.Tours.FindAsync(id);
            if (t == null) return NotFound();
            return View(t);
        }

        [HttpPost]
        public async Task<IActionResult> EditTour(Tour model)
        {
            if (!ModelState.IsValid) return View(model);
            var t = await _context.Tours.FindAsync(model.MaTour);
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
            await _context.SaveChangesAsync();
            TempData["Success"] = "Cập nhật tour thành công";
            return RedirectToAction("Tours");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Admin/DeleteTour/{id}")]
        public async Task<IActionResult> DeleteTour(int id)
        {
            var t = await _context.Tours.FindAsync(id);
            if (t == null) return NotFound();

            var images = await _context.AnhTours.Where(a => a.MaTour == id).ToListAsync();
            if (images.Any())
            {
                _context.AnhTours.RemoveRange(images);
            }

            _context.Tours.Remove(t);

            try
            {
                await _context.SaveChangesAsync();

                if (!string.IsNullOrEmpty(t.QR))
                {
                    var qrPath = Path.Combine(_env.WebRootPath, t.QR.TrimStart('/'));
                    if (System.IO.File.Exists(qrPath))
                    {
                        System.IO.File.Delete(qrPath);
                    }
                }

                TempData["Success"] = "Xóa tour thành công";
            }
            catch (DbUpdateException ex)
            {
                var innerException = ex.InnerException;
                if (innerException is Oracle.ManagedDataAccess.Client.OracleException oracleEx && oracleEx.Number == 2292)
                {
                    TempData["Error"] = "Không thể xóa tour. Có thể do đã có khách hàng đặt tour này hoặc có dữ liệu liên quan khác.";
                }
                else
                {
                    TempData["Error"] = "Đã xảy ra lỗi khi xóa tour: " + (innerException?.Message ?? ex.Message);
                }
            }

            return RedirectToAction(nameof(Tours));
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
    }
}