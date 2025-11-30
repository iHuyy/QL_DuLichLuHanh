using DuLich.Models;
using DuLich.Models.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace DuLich.Controllers.staff
{
    [Authorize(Roles = "ROLE_STAFF")]
    [Route("staff/api")]
    [ApiController]
    public class StaffApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public StaffApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task<int> MarkDepartedBookingsAsCompletedAsync(int? branchId)
        {
            var query = _context.DatTours
                .Include(d => d.Tour)
                .Where(d => d.TrangThaiDat != "Đã hủy"
                            && d.TrangThaiDat != "Hoàn thành"
                            && d.Tour != null
                            && d.Tour.TrangThai == "Hoàn thành");

            if (branchId.HasValue)
            {
                query = query.Where(d => d.Tour!.MaChiNhanh == branchId.Value);
            }

            var bookings = await query.ToListAsync();
            if (!bookings.Any())
            {
                return 0;
            }

            foreach (var booking in bookings)
            {
                booking.TrangThaiDat = "Hoàn thành";
            }

            _context.DatTours.UpdateRange(bookings);
            await _context.SaveChangesAsync();
            return bookings.Count;
        }

        private async Task<NhanVien?> GetCurrentStaffAsync()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return null;
            }
            return await _context.NhanViens.FirstOrDefaultAsync(n => n.ORACLE_USERNAME != null && n.ORACLE_USERNAME.ToUpper() == username.ToUpper());
        }

        /// <summary>
        /// Thiết lập lại context VPD/OLS cho session hiện tại (tránh ORA-28115).
        /// </summary>
        private async Task EnsureOracleSecurityContextAsync(int branchId)
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
BEGIN
  TADMIN.pkg_tour_management.set_user_context(:role_name, :branch_id);
  SA_SESSION.SET_LABEL('DULICH_OLS', :p_label);
END;";

            if (cmd is OracleCommand ocmd)
            {
                ocmd.BindByName = true;
                ocmd.Parameters.Add(new OracleParameter("role_name", OracleDbType.Varchar2) { Value = "ROLE_STAFF" });
                ocmd.Parameters.Add(new OracleParameter("branch_id", OracleDbType.Int32) { Value = branchId });
                ocmd.Parameters.Add(new OracleParameter("p_label", OracleDbType.Varchar2) { Value = "INT" });
            }
            else
            {
                var pRole = cmd.CreateParameter();
                pRole.ParameterName = "role_name";
                pRole.Value = "ROLE_STAFF";
                cmd.Parameters.Add(pRole);

                var pBranch = cmd.CreateParameter();
                pBranch.ParameterName = "branch_id";
                pBranch.Value = branchId;
                cmd.Parameters.Add(pBranch);

                var pLabel = cmd.CreateParameter();
                pLabel.ParameterName = "p_label";
                pLabel.Value = "INT";
                cmd.Parameters.Add(pLabel);
            }

            await cmd.ExecuteNonQueryAsync();
        }

                [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var staff = await GetCurrentStaffAsync();
            if (staff == null || !staff.MaChiNhanh.HasValue)
            {
                return Ok(new
                {
                    totalRevenue = 0m,
                    totalCustomers = 0,
                    totalTours = 0,
                    totalBookings = 0
                });
            }

            var branchId = staff.MaChiNhanh.Value;
            await EnsureOracleSecurityContextAsync(branchId);

            var totalRevenue = await _context.HoaDons
                .Where(h => h.TrangThai == "Đã thanh toán" && h.DatTour != null && h.DatTour.Tour != null && h.DatTour.Tour.MaChiNhanh == branchId)
                .SumAsync(h => (decimal?)(h.SoTien ?? 0)) ?? 0m;

            var totalCustomers = await _context.DatTours
                .Where(d => d.Tour != null && d.Tour.MaChiNhanh == branchId && d.MaKhachHang.HasValue)
                .Select(d => d.MaKhachHang.Value)
                .Distinct()
                .CountAsync();

            var totalTours = await _context.Tours
                .CountAsync(t => t.MaChiNhanh == branchId);

            var totalBookings = await _context.DatTours
                .CountAsync(d => d.Tour != null && d.Tour.MaChiNhanh == branchId);

            return Ok(new
            {
                totalRevenue,
                totalCustomers,
                totalTours,
                totalBookings
            });
        }

        private string GetBadgeClass(string status, string context)
        {
            var normalized = (status ?? string.Empty).Trim().ToLowerInvariant();
            if (context == "tour")
            {
                return normalized switch
                {
                    var s when s.Contains("ho?t") || s.Contains("hoat") => "success",
                    var s when s.Contains("dang") => "primary",
                    var s when s.Contains("hoàn thành") || s.Contains("hoan") => "info",
                    var s when s.Contains("hủy") || s.Contains("huy") => "danger",
                    _ => "secondary"
                };
            }

            return normalized switch
            {
                var s when s.Contains("đã xác") || s.Contains("da xac") => "success",
                var s when s.Contains("chờ") || s.Contains("cho") => "warning",
                var s when s.Contains("hủy") || s.Contains("huy") => "danger",
                var s when s.Contains("hoàn thành") || s.Contains("hoan") => "info",
                _ => "secondary"
            };
        }

        
        [HttpGet("recent-bookings")]
        public async Task<IActionResult> GetRecentBookings()
        {
            var staff = await GetCurrentStaffAsync();
            if (staff == null || !staff.MaChiNhanh.HasValue)
            {
                return Ok(new { data = new List<object>() });
            }

            await MarkDepartedBookingsAsCompletedAsync(staff.MaChiNhanh);
            await EnsureOracleSecurityContextAsync(staff.MaChiNhanh.Value);

            var bookings = await _context.DatTours
                .AsNoTracking()
                .Where(d => d.Tour != null && d.Tour.MaChiNhanh == staff.MaChiNhanh)
                .Include(d => d.Tour)
                .Include(d => d.KhachHang)
                .OrderByDescending(d => d.NgayDat)
                .Take(20)
                .Select(d => new
                {
                    id = d.MaDatTour,
                    customerName = d.KhachHang != null ? d.KhachHang.HoTen : "Khách hàng ẩn danh",
                    tourName = d.Tour != null ? d.Tour.TieuDe : "Tour ẩn danh",
                    bookingDate = d.NgayDat,
                    quantity = (d.SoNguoiLon ?? 0) + (d.SoTreEm ?? 0),
                    totalAmount = d.TongTien ?? 0,
                    status = d.TrangThaiDat ?? "Chưa xác nhận",
                    statusClass = GetBadgeClass(d.TrangThaiDat, "booking")
                })
                .ToListAsync();

            return Ok(bookings);
        }
        [HttpGet("upcoming-tours")]
        public async Task<IActionResult> GetUpcomingTours()
        {
            var staff = await GetCurrentStaffAsync();
            if (staff == null || !staff.MaChiNhanh.HasValue)
            {
                return Ok(new { data = new List<object>() });
            }

            await EnsureOracleSecurityContextAsync(staff.MaChiNhanh.Value);
            var tours = await _context.Tours
                .AsNoTracking()
                .Where(t => t.MaChiNhanh == staff.MaChiNhanh && t.ThoiGian >= DateTime.Today)
                .OrderBy(t => t.ThoiGian)
                .Take(20)
                .ToListAsync();

            var tourIds = tours.Select(t => t.MaTour).ToList();

            var bookingCounts = await _context.DatTours
                .Where(d => d.MaTour.HasValue && tourIds.Contains(d.MaTour.Value))
                .GroupBy(d => d.MaTour.Value)
                .Select(g => new { MaTour = g.Key, TotalGuests = g.Sum(d => (d.SoNguoiLon ?? 0) + (d.SoTreEm ?? 0)) })
                .ToDictionaryAsync(x => x.MaTour, x => x.TotalGuests);

            var result = tours.Select(t => new
            {
                name = t.TieuDe ?? "Không tên",
                startDate = t.ThoiGian,
                currentBookings = bookingCounts.ContainsKey(t.MaTour) ? bookingCounts[t.MaTour] : 0,
                maxCapacity = t.SoLuong ?? 0,
                status = t.TrangThai ?? "Chưa cập nhật",
                statusClass = GetBadgeClass(t.TrangThai, "tour")
            });

            return Ok(result);
        }
        [HttpGet("notifications")]
        public IActionResult GetNotifications()
        {
            var notifications = new List<object>
            {
                new { icon = "fa fa-bell", type = "success", title = "Hoạt động", message = "Hệ thống dashboard nhân viên đang hoạt động.", time = DateTime.UtcNow.ToString("HH:mm dd/MM/yyyy") },
                new { icon = "fa fa-chart-line", type = "info", title = "Doanh thu", message = "Báo cáo doanh thu tháng mới đã sẵn sàng.", time = DateTime.UtcNow.AddHours(-1).ToString("HH:mm dd/MM/yyyy") },
                new { icon = "fa fa-warning", type = "warning", title = "Cảnh báo", message = "Tour #123 sắp kết thúc, vui lòng kiểm tra.", time = DateTime.UtcNow.AddDays(-1).ToString("HH:mm dd/MM/yyyy") }
            };

            return Ok(notifications);
        }
[HttpGet("bookings")]
        public async Task<IActionResult> GetBookings()
        {
            var staff = await GetCurrentStaffAsync();
            if (staff == null || !staff.MaChiNhanh.HasValue)
            {
                return Ok(new { data = new List<object>() });
            }

            await EnsureOracleSecurityContextAsync(staff.MaChiNhanh.Value);
            var bookings = await _context.DatTours
                .AsNoTracking()
                .Where(d => d.Tour != null && d.Tour.MaChiNhanh == staff.MaChiNhanh)
                .Include(d => d.Tour)
                .Include(d => d.KhachHang)
                .Include(d => d.HoaDon)
                .OrderByDescending(d => d.NgayDat)
                .Select(d => new
                {
                    maDatTour = d.MaDatTour,
                    tenKhachHang = d.KhachHang != null ? d.KhachHang.HoTen : "N/A",
                    tenTour = d.Tour != null ? d.Tour.TieuDe : "N/A",
                    ngayDat = d.NgayDat,
                    soNguoiLon = d.SoNguoiLon ?? 0,
                    soTreEm = d.SoTreEm ?? 0,
                    ngayBatDau = d.Tour != null ? d.Tour.ThoiGian : (DateTime?)null,
                    trangThai = d.TrangThaiDat ?? "N/A",
                    tongTien = d.TongTien ?? 0,
                    maTour = d.Tour != null ? d.Tour.MaTour : 0,
                    maHoaDon = d.HoaDon != null ? d.HoaDon.MaHoaDon : (int?)null
                })
                .ToListAsync();

            return Ok(new { data = bookings });
        }

        // Danh sách khách hàng đã đặt tour thuộc chi nhánh của nhân viên (theo từng booking)
        [HttpGet("customers/bookings")]
        public async Task<IActionResult> GetCustomerBookings()
        {
            var staff = await GetCurrentStaffAsync();
            if (staff == null || !staff.MaChiNhanh.HasValue)
            {
                return Ok(new { data = new List<object>() });
            }

            await EnsureOracleSecurityContextAsync(staff.MaChiNhanh.Value);
            var branchId = staff.MaChiNhanh.Value;

            var customerBookings = await _context.DatTours
                .AsNoTracking()
                .Where(d => d.Tour != null && d.Tour.MaChiNhanh == branchId && d.MaKhachHang.HasValue)
                .Include(d => d.KhachHang)
                .Include(d => d.Tour)
                .GroupBy(d => new { d.MaKhachHang, d.KhachHang })
                .Select(g => new
                {
                    maKhachHang = g.Key.MaKhachHang ?? 0,
                    hoTen = g.Key.KhachHang != null ? g.Key.KhachHang.HoTen : "Khách hàng ẩn danh",
                    soDienThoai = g.Key.KhachHang != null ? g.Key.KhachHang.SoDienThoai : "",
                    email = g.Key.KhachHang != null ? g.Key.KhachHang.Email : "",
                    soTourDaDat = g.Count(),
                    lastBookingDate = g.Max(d => d.NgayDat),
                    lastTour = g.OrderByDescending(d => d.NgayDat).Select(d => d.Tour != null ? d.Tour.TieuDe : "Tour ẩn danh").FirstOrDefault()
                })
                .OrderByDescending(x => x.lastBookingDate)
                .ToListAsync();

            return Ok(new { data = customerBookings });
        }

        [HttpPost("bookings/confirm")]
        public async Task<IActionResult> ConfirmBooking([FromForm] int bookingId)
        {
            var staff = await GetCurrentStaffAsync();
            if (staff == null || !staff.MaChiNhanh.HasValue)
            {
                return Unauthorized("Không tìm thấy nhân viên.");
            }
            await EnsureOracleSecurityContextAsync(staff.MaChiNhanh.Value);

            var booking = await _context.DatTours
                .Include(d => d.Tour)
                .FirstOrDefaultAsync(d => d.MaDatTour == bookingId);

            if (booking == null || booking.Tour?.MaChiNhanh != staff.MaChiNhanh)
            {
                return NotFound("Booking không thuộc chi nhánh của bạn.");
            }

            if (booking.TrangThaiDat == "Chờ xác nhận")
            {
                booking.TrangThaiDat = "Đã xác nhận";
                await _context.SaveChangesAsync();
                return Ok(new { message = "Đã xác nhận booking." });
            }

            return BadRequest("Booking không ở trạng thái có thể xác nhận.");
        }

        [HttpPost("bookings/cancel")]
        public async Task<IActionResult> CancelBooking([FromForm] int bookingId)
        {
            var staff = await GetCurrentStaffAsync();
            if (staff == null || !staff.MaChiNhanh.HasValue)
            {
                return Unauthorized("Không tìm thấy nhân viên/chi nhánh.");
            }

            await EnsureOracleSecurityContextAsync(staff.MaChiNhanh.Value);
            var booking = await _context.DatTours
                .Include(d => d.Tour)
                .FirstOrDefaultAsync(d => d.MaDatTour == bookingId);

            if (booking == null || booking.Tour?.MaChiNhanh != staff.MaChiNhanh)
            {
                return NotFound("Booking không thuộc chi nhánh của bạn.");
            }

            if (booking.TrangThaiDat != "Đã hủy")
            {
                var originalStatus = booking.TrangThaiDat;
                booking.TrangThaiDat = "Đã hủy";

                // Trả lại chỗ nếu trước đó đã trừ chỗ
                if ((originalStatus == "Đã xác nhận" || originalStatus == "Chờ xác nhận") && booking.Tour != null && booking.Tour.SoLuong.HasValue)
                {
                    var totalGuests = (booking.SoNguoiLon ?? 0) + (booking.SoTreEm ?? 0);
                    booking.Tour.SoLuong += totalGuests;
                }

                await _context.SaveChangesAsync();
                return Ok(new { message = "Đã hủy booking." });
            }

            return BadRequest("Booking đã bị hủy trước đó.");
        }
    }
}
