using DuLich.Models;
using DuLich.Models.Data;
using DuLich.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;


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

        public ApiAuthController(OracleAuthService authService, ApplicationDbContext context, JwtService jwtService,IMemoryCache cache, EmailService emailService)
        {
            _authService = authService;
            _context = context;
            _jwtService = jwtService;
            _cache = cache;
            _emailService = emailService;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> MobileLogin([FromBody] LoginModel model)
        {
            var (success, role) = await _authService.ValidateLoginAsync(model.Username, model.Password);

            if (success)
            {
                // Tìm KhachHang (hoặc NhanVien) dựa trên ORACLE_USERNAME
                var user = await _context.KhachHangs
                    .FirstOrDefaultAsync(k => k.ORACLE_USERNAME == model.Username.ToUpper());

                if (user != null && role == "ROLE_CUSTOMER")
                {
                    // Tạo JWT cho mobile
                    var token = _jwtService.GenerateToken(user, role);
                    
                    // *** BẮT ĐẦU SỬA LỖI ***
                    // Trả về thêm userId để mobile app có thể lưu lại
                    return Ok(new { success = true, token, role, userId = user.MaKhachHang.ToString() });
                    // *** KẾT THÚC SỬA LỖI ***
                }
                
                // (Thêm logic cho NhanVien nếu mobile app hỗ trợ)
                
                return Unauthorized(new { success = false, message = "User is not a customer." });
            }

            return Unauthorized(new { success = false, message = "Invalid username or password." });
        }
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (string.IsNullOrEmpty(request.Username)) return BadRequest(new { success = false, message = "Vui lòng nhập tên đăng nhập." });

            var user = await _context.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME == request.Username.ToUpper());
            
            if (user == null || string.IsNullOrEmpty(user.Email))
            {
                // Trả về message chung chung để bảo mật hoặc báo lỗi
                return Ok(new { success = false, message = "Không tìm thấy tài khoản hoặc tài khoản chưa đăng ký email." });
            }

            // Tạo OTP 6 số
            var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();

            // Lưu vào Cache (hết hạn sau 180 giây)
            _cache.Set($"OTP_MOBILE_{request.Username.ToUpper()}", otp, TimeSpan.FromSeconds(180));

            // Gửi Email
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

        // 2. Kiểm tra OTP
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

        // 3. Đổi mật khẩu
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            // Kiểm tra lại OTP lần cuối để bảo mật
            if (!_cache.TryGetValue($"OTP_MOBILE_{request.Username.ToUpper()}", out string storedOtp) || storedOtp != request.Otp)
            {
                 return Ok(new { success = false, message = "Phiên xác thực đã hết hạn. Vui lòng thực hiện lại." });
            }

            var (success, message) = await _authService.ChangePasswordAsync(request.Username, request.NewPassword);

            if (success)
            {
                _cache.Remove($"OTP_MOBILE_{request.Username.ToUpper()}"); // Xóa OTP
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

        // DTO Classes
        public class ForgotPasswordRequest { public string Username { get; set; } }
        public class VerifyOtpRequest { public string Username { get; set; } public string Otp { get; set; } }
        public class ResetPasswordRequest { public string Username { get; set; } public string Otp { get; set; } public string NewPassword { get; set; } }
    }
}