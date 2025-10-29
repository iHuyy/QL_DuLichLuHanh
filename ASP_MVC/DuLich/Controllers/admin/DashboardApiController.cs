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
                        .Where(h => h.TrangThai == "Đã thanh toán")
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
        public async Task<IActionResult> MonthlyBookings()
        {
            try
            {
                var startDate = DateTime.Today.AddMonths(-11);
                var bookings = await _db.DatTours
                    .Where(b => b.NgayDat != null && b.NgayDat.Value >= startDate)
                    .GroupBy(b => new { Year = b.NgayDat.Value.Year, Month = b.NgayDat.Value.Month })
                    .Select(g => new
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        Count = g.Count()
                    })
                    .OrderBy(x => x.Year)
                    .ThenBy(x => x.Month)
                    .ToListAsync();

                var labels = new List<string>();
                var values = new List<int>();

                var current = startDate;
                for (var i = 0; i < 12; i++)
                {
                    labels.Add(current.ToString("MM/yyyy"));
                    var monthData = bookings.FirstOrDefault(b =>
                        b.Year == current.Year &&
                        b.Month == current.Month);
                    values.Add(monthData?.Count ?? 0);
                    current = current.AddMonths(1);
                }

                return Ok(new { labels, values });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi khi lấy thống kê đặt tour theo tháng", details = ex.Message });
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
                        danhGia = _db.DanhGiaTours
                            .Where(d => d.MaTour == t.MaTour)
                            .Average(d => (double?)d.SoSao) ?? 0,
                        doanhThu = _db.HoaDons
                            .Where(h => h.DatTour != null && h.DatTour.MaTour == t.MaTour && h.TrangThai == "Đã thanh toán")
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

        [HttpGet]
        public async Task<IActionResult> TourTypes()
        {
            try
            {
                var distribution = await _db.Tours
                    .GroupBy(t => t.NoiDen)
                    .Select(g => new { Type = g.Key ?? "Khác", Count = g.Count() })
                    .ToListAsync();

                return Ok(new
                {
                    labels = distribution.Select(d => d.Type).ToList(),
                    values = distribution.Select(d => d.Count).ToList()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Lỗi khi lấy thống kê loại tour", details = ex.Message });
            }
        }
    }
}