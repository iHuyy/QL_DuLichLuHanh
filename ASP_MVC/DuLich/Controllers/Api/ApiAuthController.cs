using DuLich.Models;
using DuLich.Models.Data;
using DuLich.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System;

namespace DuLich.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApiAuthController : ControllerBase
    {
        private readonly OracleAuthService _authService;
        private readonly ApplicationDbContext _context;
        private readonly JwtService _jwtService;
        private readonly IMemoryCache _cache;
        private readonly EmailService _emailService;
        private const string SECRET_KEY = "KLTN_2024_SecretKey_!@#";

        public ApiAuthController(OracleAuthService authService, ApplicationDbContext context, JwtService jwtService, IMemoryCache cache, EmailService emailService)
        {
            _authService = authService;
            _context = context;
            _jwtService = jwtService;
            _cache = cache;
            _emailService = emailService;
        }

        [HttpGet("keepalive")]
        [Authorize]
        public IActionResult KeepAlive()
        {
            return Ok(new { success = true, message = "Session refreshed." });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> MobileLogin([FromBody] LoginModel model)
        {
            // 1. Xác thực Username/Password với Oracle
            var (success, oracleRole, errorMessage) = await _authService.ValidateLoginAsync(model.Username, model.Password);

            if (!success && errorMessage == "PASSWORD_EXPIRED")
            {
                return Ok(new { success = false, require_change_password = true, message = "Mật khẩu đã hết hạn. Vui lòng đổi mật khẩu mới." });
            }

            if (success)
            {
                // 2. Tìm thông tin trong bảng KhachHang
                var user = await _context.KhachHangs
                    .FirstOrDefaultAsync(k => k.ORACLE_USERNAME == model.Username.ToUpper());

                if (user != null)
                {
                    var token = _jwtService.GenerateToken(user, "ROLE_CUSTOMER");
                    
                    return Ok(new { 
                        success = true, 
                        token, 
                        role = "ROLE_CUSTOMER", 
                        userId = user.MaKhachHang.ToString() 
                    });
                }
                
                
                return Unauthorized(new { success = false, message = "Tài khoản không tồn tại trong dữ liệu Khách hàng." });
            }

            return Unauthorized(new { success = false, message = errorMessage });
        }

        
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (string.IsNullOrEmpty(request.Username)) return BadRequest(new { success = false, message = "Vui lòng nhập tên đăng nhập." });

            var user = await _context.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME == request.Username.ToUpper());

            if (user == null || string.IsNullOrEmpty(user.Email))
            {
                return Ok(new { success = false, message = "Không tìm thấy tài khoản hoặc tài khoản chưa đăng ký email." });
            }

            var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            _cache.Set($"OTP_MOBILE_{request.Username.ToUpper()}", otp, TimeSpan.FromSeconds(180));

            try
            {
                await _emailService.SendEmailAsync(user.Email, "Mã xác thực Quên mật khẩu (Mobile)",
                    $"<h3>Mã OTP của bạn là: <b style='color:red;font-size:24px'>{otp}</b></h3><p>Mã này có hiệu lực trong 3 phút.</p>");

                return Ok(new { success = true, message = $"Mã xác thực đã được gửi tới email: {MaskEmail(user.Email)}" });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = "Lỗi gửi email: " + ex.Message });
            }
        }

        [HttpPost("verify-otp")]
        [AllowAnonymous]
        public IActionResult VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            if (_cache.TryGetValue($"OTP_MOBILE_{request.Username.ToUpper()}", out string storedOtp))
            {
                if (storedOtp == request.Otp)
                {
                    return Ok(new { success = true, message = "Mã xác thực chính xác." });
                }
            }
            return Ok(new { success = false, message = "Mã xác thực không đúng hoặc đã hết hạn." });
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (!_cache.TryGetValue($"OTP_MOBILE_{request.Username.ToUpper()}", out string storedOtp) || storedOtp != request.Otp)
            {
                return Ok(new { success = false, message = "Phiên xác thực đã hết hạn. Vui lòng thực hiện lại." });
            }

            var (success, message) = await _authService.ChangePasswordAsync(request.Username, request.NewPassword);

            if (success)
            {
                _cache.Remove($"OTP_MOBILE_{request.Username.ToUpper()}");
                return Ok(new { success = true, message = "Đổi mật khẩu thành công." });
            }

            return Ok(new { success = false, message = message });
        }

        private string MaskEmail(string email)
        {
            if (string.IsNullOrEmpty(email) || !email.Contains("@")) return email;
            var parts = email.Split('@');
            if (parts[0].Length > 2)
                return parts[0].Substring(0, 2) + "***@" + parts[1];
            return "***@" + parts[1];
        }

        [HttpPost("send-register-otp")]
        [AllowAnonymous]
        public async Task<IActionResult> SendRegisterOtp([FromBody] SendRegisterOtpRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Username))
                    return Ok(new { success = false, message = "Vui lòng nhập Email và Tên đăng nhập." });

                var countEmailKhach = await _context.KhachHangs.CountAsync(k => k.Email == request.Email);
                var countEmailNV = await _context.NhanViens.CountAsync(n => n.Email == request.Email);
                
                if (countEmailKhach > 0 || countEmailNV > 0) 
                    return Ok(new { success = false, message = "Email này đã được sử dụng." });

                var countUserKhach = await _context.KhachHangs.CountAsync(k => k.ORACLE_USERNAME == request.Username.ToUpper());
                var countUserNV = await _context.NhanViens.CountAsync(n => n.ORACLE_USERNAME == request.Username.ToUpper());

                if (countUserKhach > 0 || countUserNV > 0) 
                    return Ok(new { success = false, message = "Tên đăng nhập đã tồn tại." });

                var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
                long expiryTime = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
                var dataToSign = $"{request.Email}|{otp}|{expiryTime}";
                var signature = ComputeHmacSha256(dataToSign, SECRET_KEY);

                await _emailService.SendEmailAsync(request.Email, "Mã xác thực Đăng ký",
                    $"<h3>Mã OTP đăng ký của bạn là: <b style='color:red;font-size:24px'>{otp}</b></h3><p>Mã có hiệu lực 5 phút.</p>");

                return Ok(new
                {
                    success = true,
                    message = $"Đã gửi OTP tới {MaskEmail(request.Email)}",
                    otp_hash = signature,
                    otp_expiry = expiryTime
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending OTP: {ex}");
                return Ok(new { success = false, message = "Lỗi gửi email: " + ex.Message });
            }
        }

        private string ComputeHmacSha256(string data, string key)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
                return Convert.ToBase64String(hash);
            }
        }

        public class ForgotPasswordRequest { public string Username { get; set; } = string.Empty; }
        public class VerifyOtpRequest { public string Username { get; set; } = string.Empty; public string Otp { get; set; } = string.Empty; }
        public class ResetPasswordRequest { public string Username { get; set; } = string.Empty; public string Otp { get; set; } = string.Empty; public string NewPassword { get; set; } = string.Empty; }

        public class SendRegisterOtpRequest
        {
            [JsonPropertyName("username")]
            public string Username { get; set; }
            [JsonPropertyName("email")]
            public string Email { get; set; }
        }
    }
}