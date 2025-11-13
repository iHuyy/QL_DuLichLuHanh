using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DuLich.Models.Data;
using DuLich.Models;

namespace DuLich.Controllers.Api
{
    [ApiController]
    [Route("api/sessions")]
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
        [Authorize]
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

            // Match by UserId only so sessions created by other clients (mobile) with
            // different UserType string still show up. We still require IS_ACTIVE = 'Y'.
            var sessions = await _context.UserSessions
                .Where(s => s.UserId == userId && s.IsActive == "Y")
                .OrderByDescending(s => s.LoginTime)
                .ToListAsync();

            var result = sessions
                .Where(s => s.SessionId != currentSessionId)
                .Select(s => new {
                    session_id = s.SessionId,
                    device_type = s.DeviceType,
                    device_info = s.DeviceInfo,
                    login_time = s.LoginTime
                });

            return Ok(new { sessions = result });
        }

        [HttpPost("logout-remote")]
        [Authorize]
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

            // Verify the session belongs to the current user (by UserId). Don't
            // require exact UserType string match because mobile clients may store
            // different values (e.g. ROLE_CUSTOMER, CUSTOMER, etc.).
            var session = await _context.UserSessions.FirstOrDefaultAsync(s => s.SessionId == request.session_id_to_logout && s.UserId == userId && s.IsActive == "Y");
            if (session == null)
            {
                return NotFound(new { error = "Session not found or already inactive" });
            }

            // Delete the session row so it no longer counts against SESSIONS_PER_USER
            _context.UserSessions.Remove(session);
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }
    }
}
