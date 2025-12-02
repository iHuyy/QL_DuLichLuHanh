using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using DuLich.Models.Data;
using DuLich.Models;

namespace DuLich.Controllers.Api
{
    [ApiController]
    [Route("api/sessions")]
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme + "," + JwtBearerDefaults.AuthenticationScheme)]
    public class SessionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SessionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public class RemoteLogoutRequest
        {
            public string? session_id_to_logout { get; set; }
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActiveSessions()
        {
            var username = User?.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            int userId;

            var customer = await _context.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME != null && k.ORACLE_USERNAME.ToUpper() == username.ToUpper());
            if (customer != null)
            {
                userId = customer.MaKhachHang;
            }
            else
            {
                var staff = await _context.NhanViens.FirstOrDefaultAsync(n => n.ORACLE_USERNAME != null && n.ORACLE_USERNAME.ToUpper() == username.ToUpper());
                if (staff != null)
                {
                    userId = staff.MaNhanVien;
                }
                else
                {
                    return Unauthorized();
                }
            }

            var currentSessionId = Request.Cookies["USER_SESSION_ID"];

            var sessions = await _context.UserSessions
                .Where(s => s.UserId == userId && s.IsActive == "Y")
                .OrderByDescending(s => s.LoginTime)
                .ToListAsync();

            var result = sessions
                .Where(s => s.SessionId != currentSessionId)
                .Select(s => new
                {
                    session_id = s.SessionId,
                    device_type = s.DeviceType,
                    device_info = s.DeviceInfo,
                    login_time = s.LoginTime
                });

            return Ok(new { sessions = result });
        }

        [HttpPost("logout-remote")]
        public async Task<IActionResult> LogoutRemote([FromBody] RemoteLogoutRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.session_id_to_logout))
                return BadRequest(new { error = "session_id_to_logout is required" });

            var username = User?.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            int userId;

            var customer = await _context.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME != null && k.ORACLE_USERNAME.ToUpper() == username.ToUpper());
            if (customer != null)
            {
                userId = customer.MaKhachHang;
            }
            else
            {
                var staff = await _context.NhanViens.FirstOrDefaultAsync(n => n.ORACLE_USERNAME != null && n.ORACLE_USERNAME.ToUpper() == username.ToUpper());
                if (staff != null)
                {
                    userId = staff.MaNhanVien;
                }
                else
                {
                    return Unauthorized();
                }
            }

            var session = await _context.UserSessions.FirstOrDefaultAsync(s => s.SessionId == request.session_id_to_logout && s.UserId == userId && s.IsActive == "Y");
            if (session == null)
            {
                return NotFound(new { error = "Session not found or already inactive" });
            }

            session.IsActive = "N";
            session.LastActivity = DateTime.UtcNow;
            _context.UserSessions.Update(session);
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }
    }
}
