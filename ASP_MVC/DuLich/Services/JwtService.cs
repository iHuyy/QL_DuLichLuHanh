using DuLich.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace DuLich.Services
{
    public class JwtService
    {
        private readonly RSAService _rsaService;
        private readonly IConfiguration _config;

        public JwtService(RSAService rsaService, IConfiguration config)
        {
            _rsaService = rsaService;
            _config = config;
        }

        public string GenerateToken(KhachHang user, string userRole)
        {
            var jwtSettings = _config.GetSection("Jwt");
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];
            int expiryMinutes = int.Parse(jwtSettings["WebTokenExpiryMinutes"] ?? "60");
            
            var privateKey = _rsaService.GetPrivateKey();
            var credentials = new SigningCredentials(new RsaSecurityKey(privateKey), SecurityAlgorithms.RsaSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.ORACLE_USERNAME),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, user.ORACLE_USERNAME),
                new Claim(ClaimTypes.Role, userRole),
                new Claim("MaKhachHang", user.MaKhachHang.ToString()),
                new Claim("UserType", "CUSTOMER")
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.Now.AddMinutes(expiryMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // Bạn có thể thêm một hàm GenerateToken cho NhanVien nếu Mobile app của Staff/Admin cũng dùng JWT
        // public string GenerateToken(NhanVien user, string userRole) { ... }
    }
}