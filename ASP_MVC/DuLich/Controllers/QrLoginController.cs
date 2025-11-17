using DuLich.Models;
using DuLich.Models.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DuLich.Services;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json.Serialization;

namespace DuLich.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QrLoginController : BaseController
    {
        private readonly JwtService _jwtService; // Vẫn giữ để xác thực mobile

        public QrLoginController(ApplicationDbContext context, JwtService jwtService) : base(context)
        {
            _jwtService = jwtService;
        }

        /// <summary>
        /// Web (chưa đăng nhập) gọi API này.
        /// </summary>
        [HttpGet("generate-anonymous-qr")]
        [AllowAnonymous]
        public async Task<IActionResult> GenerateQrCode()
        {
            var sessionKey = Guid.NewGuid().ToString(); // Đây là token QR (ví dụ: abc-123)
            var qrLogin = new QR_Login
            {
                SessionKey = sessionKey,
                IsUsed = 0, // 0 = PENDING
                CreatedAt = DateTime.Now,
                UserId = null // USERNAME = NULL
            };
            _context.QR_Logins.Add(qrLogin);
            await _context.SaveChangesAsync();

            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(sessionKey, QRCodeGenerator.ECCLevel.Q);
            using (var qrCode = new PngByteQRCode(qrCodeData))
            {
                byte[] qrCodeImage = qrCode.GetGraphic(20);
                return Json(new { qrToken = sessionKey, qrCodeImage = Convert.ToBase64String(qrCodeImage) });
            }
        }

        /// <summary>
        /// Web (đang "hỏi") gọi API này mỗi 3 giây.
        /// </summary>
        [HttpGet("poll-qr-status/{qrToken}")]
        [AllowAnonymous]
        public async Task<IActionResult> PollQrStatus(string qrToken)
        {
            var qrLogin = await _context.QR_Logins.FirstOrDefaultAsync(q => q.SessionKey == qrToken);

            if (qrLogin == null || qrLogin.CreatedAt < DateTime.Now.AddMinutes(-5)) // 5 minute expiry
            {
                return Json(new { status = "EXPIRED" });
            }

            // (IsUsed = 1) -> STATUS = 'COMPLETED' và UserId đã được gán
            if (qrLogin.IsUsed == 1 && qrLogin.UserId.HasValue)
            {
                var user = await _context.KhachHangs.FindAsync(qrLogin.UserId.Value);
                if (user != null)
                {
                    // *** BẮT ĐẦU SỬA LỖI ***
                    // THAY VÌ TẠO JWT, chúng ta tạo một UserSession và set cookie,
                    // giống hệt như luồng đăng nhập username/password
                    
                    try
                    {
                        var sessionId = Guid.NewGuid().ToString("N");

                        // Xóa các session WEB cũ
                        var prev = _context.UserSessions
                            .Where(s => s.UserId == user.MaKhachHang && s.DeviceType == "WEB")
                            .ToList();
                        if (prev.Any())
                        {
                            _context.UserSessions.RemoveRange(prev);
                        }

                        // Tạo UserSession mới
                        var userSession = new UserSession
                        {
                            SessionId = sessionId,
                            UserId = user.MaKhachHang,
                            UserType = "CUSTOMER",
                            DeviceType = "WEB",
                            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                            DeviceInfo = Request.Headers["User-Agent"].ToString(),
                            IsActive = "Y",
                            LoginTime = DateTime.UtcNow,
                            LastActivity = DateTime.UtcNow
                        };

                        _context.UserSessions.Add(userSession);
                        
                        // Đánh dấu token QR này là đã được sử dụng (Consumed)
                        qrLogin.IsUsed = 2; // 2 = Đã tiêu thụ (Consumed)
                        await _context.SaveChangesAsync();

                        // Set cookie USER_SESSION_ID cho trình duyệt
                        var cookieOptions = new Microsoft.AspNetCore.Http.CookieOptions
                        {
                            HttpOnly = true,
                            Secure = Request.IsHttps,
                            // Lax là cần thiết cho redirect
                            SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax 
                        };
                        Response.Cookies.Append("USER_SESSION_ID", sessionId, cookieOptions);

                        // Trả về status "Authenticated"
                        return Json(new { status = "COMPLETED" }); 
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("QR login: failed to create session: " + ex.Message);
                        return Json(new { status = "ERROR", message = "Session creation failed." });
                    }
                    // *** KẾT THÚC SỬA LỖI ***
                }
                else
                {
                    return Json(new { status = "ERROR", message = "User data not found." });
                }
            }

            if (qrLogin.IsUsed == 2)
            {
                return Json(new { status = "EXPIRED" }); // Đã được tiêu thụ
            }

            return Json(new { status = "PENDING" });
        }


        /// <summary>
        /// Mobile (đã đăng nhập) gọi API này sau khi quét.
        /// </summary>
        [HttpPost("approve-qr-login")]
[Authorize(Policy = "MobileUser")]
public async Task<IActionResult> ApproveQrLogin([FromBody] QrApproveRequest request)
{
    // 1. Debug xem nhận được gì từ Flutter
    Console.WriteLine($"[DEBUG] User: {User.Identity?.Name}");
    Console.WriteLine($"[DEBUG] Token received: '{request.QrToken}'");

    var oracleUsername = User.Identity?.Name;
    if (string.IsNullOrEmpty(oracleUsername)) return Unauthorized();

    var user = await _context.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME == oracleUsername);
    if (user == null) return Unauthorized("User not found.");

    if (string.IsNullOrEmpty(request.QrToken))
    {
        return BadRequest(new { status = "ERROR", message = "Token is empty/null. Check JSON binding." });
    }

    // 2. Tìm token trong DB (Chỉ tìm theo SessionKey trước)
    var qrLogin = await _context.QR_Logins.FirstOrDefaultAsync(q => q.SessionKey == request.QrToken);

    // 3. Kiểm tra từng điều kiện và báo lỗi riêng biệt
    if (qrLogin == null)
    {
        Console.WriteLine($"[DEBUG] Token '{request.QrToken}' not found in DB.");
        return NotFound(new { status = "ERROR", message = "QR Token incorrect (Not found in DB)." });
    }

    if (qrLogin.IsUsed != 0)
    {
         Console.WriteLine($"[DEBUG] Token '{request.QrToken}' already used. Status: {qrLogin.IsUsed}");
         return BadRequest(new { status = "ERROR", message = "QR Code already scanned/used." });
    }

    // Lưu ý: Kiểm tra Timezone. Dùng DateTime.Now nếu server và DB cùng múi giờ.
    if (qrLogin.CreatedAt <= DateTime.Now.AddMinutes(-5))
    {
        Console.WriteLine($"[DEBUG] Token expired. Created: {qrLogin.CreatedAt}, Now: {DateTime.Now}");
        return BadRequest(new { status = "ERROR", message = "QR Code expired." });
    }

    // 4. Nếu qua hết các ải trên -> OK
    qrLogin.UserId = user.MaKhachHang;
    qrLogin.IsUsed = 1; // COMPLETED
    await _context.SaveChangesAsync();

    Console.WriteLine("[SUCCESS] Login Approved!");
    return Ok(new { status = "APPROVED" });
}
    
    public class QrApproveRequest
    {
        [JsonPropertyName("qrToken")]
        public string QrToken { get; set; }
    }
}
}