using DuLich.Models;
using DuLich.Models.Data;
using DuLich.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DuLich.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApiAuthController : ControllerBase
    {
        private readonly OracleAuthService _authService;
        private readonly ApplicationDbContext _context;
        private readonly JwtService _jwtService;

        public ApiAuthController(OracleAuthService authService, ApplicationDbContext context, JwtService jwtService)
        {
            _authService = authService;
            _context = context;
            _jwtService = jwtService;
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
    }
}