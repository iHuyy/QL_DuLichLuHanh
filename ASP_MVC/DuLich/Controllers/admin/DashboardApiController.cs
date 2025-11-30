using DuLich.Models;
using DuLich.Models.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DuLich.Controllers.admin
{
    [Authorize(Roles = "ROLE_ADMIN")]
    [Route("admin/api/[action]")]
    [ApiController]
    public class DashboardApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public DashboardApiController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> SystemStats()
        {
            try
            {
                var today = DateTime.Today;
                var stats = new
                {
                    totalUsers = await _db.KhachHangs.CountAsync(),
                    totalTours = await _db.Tours.CountAsync(),
                    todayBookings = await _db.DatTours
                        .CountAsync(d => d.NgayDat != null && d.NgayDat.Value.Date == today),
                    totalRevenue = await _db.HoaDons
                        //.Where(h => h.TrangThai == "Đã thanh toán") // Tạm thời bỏ lọc để debug
                        .Select(h => h.SoTien ?? 0)
                        .SumAsync(),
                    activeTours = await _db.Tours
                        .CountAsync(t => t.ThoiGian != null && t.ThoiGian >= today),
                    expiredTours = await _db.Tours
                        .CountAsync(t => t.ThoiGian != null && t.ThoiGian < today)
                };
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi khi lấy thống kê hệ thống", details = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> PopularTours()
        {
            try
            {
                var tours = await _db.Tours
                    .Select(t => new
                    {
                        tenTour = t.TieuDe,
                        soLuotDat = _db.DatTours.Count(d => d.MaTour == t.MaTour),
                        doanhThu = _db.HoaDons
                            .Where(h => h.DatTour != null && h.DatTour.MaTour == t.MaTour) // Tạm thời bỏ lọc trạng thái để debug
                            .Sum(h => (decimal?)h.SoTien) ?? 0
                    })
                    .OrderByDescending(t => t.soLuotDat)
                    .Take(10)
                    .ToListAsync();

                return Ok(tours);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi khi lấy tours phổ biến", details = ex.Message });
            }
        }
    }
}