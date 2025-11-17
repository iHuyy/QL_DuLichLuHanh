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
        private readonly JwtService _jwtService;

        // Lưu ý: Đã bỏ OracleAuthService vì không dùng đến trong flow này (đã xác thực qua mobile)
        public QrLoginController(ApplicationDbContext context, JwtService jwtService) : base(context)
        {
            _jwtService = jwtService;
        }

        /// <summary>
        /// Web (chưa đăng nhập) gọi API này để lấy ảnh QR.
        /// </summary>
        [HttpGet("generate-anonymous-qr")]
        [AllowAnonymous]
        public async Task<IActionResult> GenerateQrCode()
        {
            var sessionKey = Guid.NewGuid().ToString();
            var qrLogin = new QR_Login
            {
                SessionKey = sessionKey,
                IsUsed = 0, // 0 = PENDING
                CreatedAt = DateTime.Now,
                UserId = null
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
        /// Web (đang "hỏi") gọi API này mỗi 2-3 giây để kiểm tra trạng thái.
        /// </summary>
        [HttpGet("poll-qr-status/{qrToken}")]
        [AllowAnonymous]
        public async Task<IActionResult> PollQrStatus(string qrToken)
        {
            var qrLogin = await _context.QR_Logins.FirstOrDefaultAsync(q => q.SessionKey == qrToken);

            // 1. Kiểm tra tồn tại và hết hạn
            if (qrLogin == null || qrLogin.CreatedAt < DateTime.Now.AddMinutes(-5))
            {
                return Json(new { status = "EXPIRED" });
            }

            // 2. Nếu Mobile App đã quét và Approve (IsUsed = 1)
            if (qrLogin.IsUsed == 1 && qrLogin.UserId.HasValue)
            {
                var user = await _context.KhachHangs.FindAsync(qrLogin.UserId.Value);
                if (user != null)
                {
                    // *** LOGIC ĐĂNG NHẬP WEB ***
                    try
                    {
                        // A. Tạo Session ID mới cho Web
                        var sessionId = Guid.NewGuid().ToString("N");

                        // B. Xóa các session WEB cũ của user này để tránh rác (tùy chọn)
                        var prevSessions = _context.UserSessions
                            .Where(s => s.UserId == user.MaKhachHang && s.DeviceType == "WEB")
                            .ToList();
                        if (prevSessions.Any())
                        {
                            _context.UserSessions.RemoveRange(prevSessions);
                        }

                        // C. Tạo bản ghi Session mới vào DB
                        var userSession = new UserSession
                        {
                            SessionId = sessionId,
                            UserId = user.MaKhachHang,
                            UserType = "CUSTOMER", // Mặc định là Customer
                            DeviceType = "WEB",
                            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                            DeviceInfo = Request.Headers["User-Agent"].ToString(),
                            IsActive = "Y",
                            LoginTime = DateTime.UtcNow,
                            LastActivity = DateTime.UtcNow
                        };

                        _context.UserSessions.Add(userSession);

                        // D. Đánh dấu mã QR này là đã hoàn tất (Consumed) để không dùng lại được
                        qrLogin.IsUsed = 2; 
                        
                        await _context.SaveChangesAsync();

                        // E. QUAN TRỌNG: Gán Cookie vào Response để trình duyệt lưu lại
                        var cookieOptions = new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = true, // Đặt false nếu chạy localhost http thường, true nếu https
                            SameSite = SameSiteMode.Lax, // Lax cho phép redirect
                            Expires = DateTime.Now.AddDays(1)
                        };
                        
                        // Tên cookie phải khớp với logic kiểm tra Session trong Middleware (ví dụ: USER_SESSION_ID)
                        Response.Cookies.Append("USER_SESSION_ID", sessionId, cookieOptions);

                        // Trả về status COMPLETED để JS chuyển trang
                        return Json(new { status = "COMPLETED", message = "Login successful" });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("QR login error: " + ex.Message);
                        return Json(new { status = "ERROR", message = "Server error creating session." });
                    }
                }
                else
                {
                    return Json(new { status = "ERROR", message = "User not found." });
                }
            }

            // 3. Nếu trạng thái đã hoàn tất (2)
            if (qrLogin.IsUsed == 2)
            {
                return Json(new { status = "EXPIRED", message = "QR Code already used." });
            }

            // 4. Nếu chưa ai quét
            return Json(new { status = "PENDING" });
        }

        /// <summary>
        /// Mobile (đã đăng nhập) gọi API này sau khi quét để xác nhận.
        /// </summary>
        [HttpPost("approve-qr-login")]
        [Authorize(Policy = "MobileUser")] // Yêu cầu JWT Token từ Mobile
        public async Task<IActionResult> ApproveQrLogin([FromBody] QrApproveRequest request)
        {
            // Lấy username từ JWT (ClaimsPrincipal)
            var oracleUsername = User.Identity?.Name;
            if (string.IsNullOrEmpty(oracleUsername)) return Unauthorized();

            var user = await _context.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME == oracleUsername);
            if (user == null) return Unauthorized(new { message = "User info not found" });

            if (string.IsNullOrEmpty(request.QrToken))
            {
                return BadRequest(new { status = "ERROR", message = "Token is required" });
            }

            var qrLogin = await _context.QR_Logins.FirstOrDefaultAsync(q => q.SessionKey == request.QrToken);

            if (qrLogin == null)
            {
                return NotFound(new { status = "ERROR", message = "Invalid QR Token" });
            }

            if (qrLogin.IsUsed != 0)
            {
                return BadRequest(new { status = "ERROR", message = "QR Code already used or expired" });
            }

            if (qrLogin.CreatedAt <= DateTime.Now.AddMinutes(-5))
            {
                return BadRequest(new { status = "ERROR", message = "QR Code expired" });
            }

            // Cập nhật trạng thái QR thành "Đã quét/Đã duyệt" (IsUsed = 1)
            qrLogin.UserId = user.MaKhachHang;
            qrLogin.IsUsed = 1; 
            
            await _context.SaveChangesAsync();

            return Ok(new { status = "APPROVED", message = "Login approved successfully" });
        }

        public class QrApproveRequest
        {
            [JsonPropertyName("qrToken")]
            public string QrToken { get; set; }
        }
    }
}