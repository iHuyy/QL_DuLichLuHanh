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
            var user = await _context.KhachHangs.FirstOrDefaultAsync(k => k.ORACLE_USERNAME == request.Username.ToUpper());
            if (user == null || string.IsNullOrEmpty(user.Email))
            {
                // Trả về thành công giả để bảo mật, hoặc báo lỗi tùy chính sách
                return NotFound(new { success = false, message = "Không tìm thấy tài khoản hoặc tài khoản chưa đăng ký email." });
            }

            // Tạo mã OTP 6 số ngẫu nhiên
            var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();

            // Lưu OTP vào Cache trong 180 giây (3 phút)
            _cache.Set($"OTP_{request.Username.ToUpper()}", otp, TimeSpan.FromSeconds(180));

            // Gửi Email
            try
            {
                string subject = "Mã xác thực Quên mật khẩu - DuLich App";
                string body = $"<h3>Mã xác thực của bạn là: <b style='color:red; font-size:20px;'>{otp}</b></h3>" +
                              $"<p>Mã này có hiệu lực trong vòng 3 phút. Vui lòng không chia sẻ cho ai khác.</p>";
                
                await _emailService.SendEmailAsync(user.Email, subject, body);
                
                // Mask email để hiển thị cho user biết (v.d: a***@gmail.com)
                string maskedEmail = string.Format("{0}****{1}", user.Email[0], user.Email.Substring(user.Email.IndexOf('@')));
                
                return Ok(new { success = true, message = $"Mã xác thực đã được gửi đến {maskedEmail}" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Lỗi gửi email: " + ex.Message });
            }
        }

        // BƯỚC 2: Xác thực OTP
        [HttpPost("verify-otp")]
        [AllowAnonymous]
        public IActionResult VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            if (_cache.TryGetValue($"OTP_{request.Username.ToUpper()}", out string storedOtp))
            {
                if (storedOtp == request.Otp)
                {
                    return Ok(new { success = true, message = "Mã xác thực chính xác." });
                }
            }
            return BadRequest(new { success = false, message = "Mã xác thực không đúng hoặc đã hết hạn." });
        }

        // BƯỚC 3: Đổi mật khẩu mới
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            // Kiểm tra lại OTP lần cuối để đảm bảo an toàn (tránh việc user bỏ qua bước verify)
            if (!_cache.TryGetValue($"OTP_{request.Username.ToUpper()}", out string storedOtp) || storedOtp != request.Otp)
            {
                 return BadRequest(new { success = false, message = "Phiên giao dịch hết hạn. Vui lòng thử lại từ đầu." });
            }

            // Gọi hàm đổi mật khẩu của OracleAuthService
            var (success, message) = await _authService.ChangePasswordAsync(request.Username, request.NewPassword);

            if (success)
            {
                // Xóa OTP sau khi đổi thành công
                _cache.Remove($"OTP_{request.Username.ToUpper()}");
                return Ok(new { success = true, message = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại." });
            }
            
            return BadRequest(new { success = false, message = message });
        }
        public class ForgotPasswordRequest { public string Username { get; set; } }
        public class VerifyOtpRequest { public string Username { get; set; } public string Otp { get; set; } }
        public class ResetPasswordRequest { public string Username { get; set; } public string Otp { get; set; } public string NewPassword { get; set; } }
    }
}