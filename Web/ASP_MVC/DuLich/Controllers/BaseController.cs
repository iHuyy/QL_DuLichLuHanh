using DuLich.Models;
using DuLich.Models.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace DuLich.Controllers
{
    public abstract class BaseController : Controller
    {
        protected readonly ApplicationDbContext _context;

        protected BaseController(ApplicationDbContext context)
        {
            _context = context;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            await SetUserContext();
            await next();
        }

        /// <summary>
        /// Mark bookings whose tours have finished as completed (based on tour status).
        /// </summary>
        protected async Task<int> UpdateDepartedBookingsToCompletedAsync()
        {
            var bookingsToComplete = await _context.DatTours
                .Include(d => d.Tour)
                .Where(d => d.TrangThaiDat != "Đã hủy"
                            && d.TrangThaiDat != "Hoàn thành"
                            && d.Tour != null
                            && d.Tour.TrangThai == "Hoàn thành")
                .ToListAsync();

            if (!bookingsToComplete.Any())
            {
                return 0;
            }

            foreach (var booking in bookingsToComplete)
            {
                booking.TrangThaiDat = "Hoàn thành";
            }

            _context.DatTours.UpdateRange(bookingsToComplete);
            await _context.SaveChangesAsync();
            return bookingsToComplete.Count;
        }

        private async Task SetUserContext()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var chiNhanhIdStr = User.FindFirst("MaChiNhanh")?.Value;
            int.TryParse(chiNhanhIdStr, out var chiNhanhId);

            if (!string.IsNullOrEmpty(role))
            {
                var conn = _context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                {
                    await conn.OpenAsync();
                }
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "BEGIN TADMIN.pkg_tour_management.set_user_context(:role_name, :branch_id); END;";

                var roleParam = cmd.CreateParameter();
                roleParam.ParameterName = "role_name";
                roleParam.Value = role;
                cmd.Parameters.Add(roleParam);

                var branchParam = cmd.CreateParameter();
                branchParam.ParameterName = "branch_id";
                branchParam.Value = chiNhanhId == 0 ? (object)DBNull.Value : chiNhanhId;
                cmd.Parameters.Add(branchParam);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        protected async Task<int> GetReservedSeatCountAsync(int tourId)
        {
            var bookings = await _context.DatTours
                .AsNoTracking()
                .Where(d => d.MaTour == tourId)
                .Select(d => new
                {
                    d.TrangThaiDat,
                    Adults = d.SoNguoiLon ?? 0,
                    Children = d.SoTreEm ?? 0
                })
                .ToListAsync();

            return bookings
                .Where(b => !IsCancellationStatus(b.TrangThaiDat))
                .Sum(b => b.Adults + b.Children);
        }

        protected static bool IsCancellationStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return false;

            var normalized = RemoveDiacritics(status).ToLowerInvariant();
            return normalized.Contains("huy");
        }

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
