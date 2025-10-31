using DuLich.Models;
using DuLich.Models.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DuLich.Controllers
{
    public class QrLoginController : BaseController
    {
        public QrLoginController(ApplicationDbContext context) : base(context)
        {
        }

        [HttpGet]
        public async Task<IActionResult> GenerateQrCode()
        {
            var sessionKey = Guid.NewGuid().ToString();
            var qrLogin = new QR_Login
            {
                SessionKey = sessionKey,
                IsUsed = 0,
                CreatedAt = DateTime.Now
            };
            _context.QR_Logins.Add(qrLogin);
            await _context.SaveChangesAsync();

            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(sessionKey, QRCodeGenerator.ECCLevel.Q);
            using (var qrCode = new PngByteQRCode(qrCodeData))
            {
                byte[] qrCodeImage = qrCode.GetGraphic(20);
                return Json(new { sessionKey, qrCodeImage = Convert.ToBase64String(qrCodeImage) });
            }
        }

        [HttpGet]
        public async Task<IActionResult> CheckQrLoginStatus(string sessionKey)
        {
            var qrLogin = await _context.QR_Logins.FirstOrDefaultAsync(q => q.SessionKey == sessionKey);

            if (qrLogin == null || qrLogin.CreatedAt < DateTime.Now.AddMinutes(-5)) // 5 minute expiry
            {
                return Json(new { status = "Expired" });
            }

            if (qrLogin.IsUsed == 1 && qrLogin.UserId.HasValue)
            {
                var user = await _context.KhachHangs.FindAsync(qrLogin.UserId.Value);
                if (user != null)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, user.ORACLE_USERNAME),
                        new Claim(ClaimTypes.Role, "ROLE_CUSTOMER") 
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var authProperties = new AuthenticationProperties();

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    return Json(new { status = "Authenticated" });
                }
            }

            return Json(new { status = "Pending" });
        }

        [HttpPost]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<IActionResult> AuthenticateQrSession([FromBody] QrAuthRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.SessionKey) || request.UserId == 0)
            {
                return BadRequest();
            }

            var qrLogin = await _context.QR_Logins.FirstOrDefaultAsync(q => q.SessionKey == request.SessionKey);
            if (qrLogin == null || qrLogin.IsUsed == 1)
            {
                return NotFound();
            }

            var user = await _context.KhachHangs.FindAsync(request.UserId);
            if (user == null)
            {
                return NotFound();
            }

            qrLogin.IsUsed = 1;
            qrLogin.UserId = user.MaKhachHang;
            await _context.SaveChangesAsync();

            return Ok();
        }
    }

    public class QrAuthRequest
    {
        public string SessionKey { get; set; }
        public int UserId { get; set; }
    }
}
