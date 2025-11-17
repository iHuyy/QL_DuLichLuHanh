using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies; // Cần namespace này
using DuLich.Models.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic; // Cần cho List<Claim>
using System.Security.Claims;    // Cần cho Claim, ClaimsIdentity

namespace DuLich.Middlewares
{
    public class SessionValidationMiddleware
    {
        private readonly RequestDelegate _next;

        public SessionValidationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
        {
            try
            {
                var path = context.Request.Path.Value ?? string.Empty;

                // Bỏ qua các file tĩnh và API login để tránh vòng lặp
                if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("/Customer/Login", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("/Customer/Register", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("/css", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("/js", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("/images", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("/favicon.ico", StringComparison.OrdinalIgnoreCase))
                {
                    await _next(context);
                    return;
                }

                var sessionId = context.Request.Cookies["USER_SESSION_ID"];
                // Console.WriteLine($"SessionValidationMiddleware: incoming path={path}, USER_SESSION_ID={(sessionId ?? "<none>")}");

                if (!string.IsNullOrEmpty(sessionId))
                {
                    var sess = await db.UserSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
                    
                    // 1. Nếu Session trong DB không hợp lệ -> Đăng xuất và xóa cookie
                    if (sess == null || sess.IsActive != "Y")
                    {
                        Console.WriteLine("Session invalid or inactive. Logging out.");
                        try
                        {
                            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        }
                        catch { }
                        context.Response.Cookies.Delete("USER_SESSION_ID");
                        
                        // Chỉ redirect nếu không phải đang ở trang Login
                        if (!path.Contains("/Login"))
                        {
                            context.Response.Redirect("/Customer/Login");
                            return; 
                        }
                    }
                    else
                    {
                        // 2. Session DB hợp lệ. Cập nhật thời gian hoạt động
                        sess.LastActivity = DateTime.UtcNow;
                        db.UserSessions.Update(sess);
                        await db.SaveChangesAsync();

                        // *** SỬA LỖI QUAN TRỌNG Ở ĐÂY ***
                        // Nếu Session DB ngon lành nhưng ASP.NET Core User chưa đăng nhập
                        // (Trường hợp login QR xong redirect, cookie auth chưa kịp ăn hoặc bị mất)
                        if (context.User?.Identity?.IsAuthenticated != true && sess.UserId.HasValue)
                        {
                            Console.WriteLine($"[AUTO-LOGIN] Valid DB Session found ({sess.SessionId}), re-hydrating User Identity.");

                            // Tìm User để lấy Username/Role
                            // Lưu ý: Logic này giả định user là KhachHang. Nếu có cả NhanVien cần check UserType
                            string username = "UNKNOWN";
                            string role = "ROLE_CUSTOMER";

                            if (sess.UserType == "CUSTOMER")
                            {
                                var kh = await db.KhachHangs.FindAsync(sess.UserId.Value);
                                if (kh != null) username = kh.ORACLE_USERNAME;
                            }
                            else if (sess.UserType == "STAFF" || sess.UserType == "ADMIN")
                            {
                                var nv = await db.NhanViens.FindAsync(sess.UserId.Value);
                                if (nv != null) 
                                {
                                    username = nv.ORACLE_USERNAME;
                                    role = sess.UserType == "ADMIN" ? "ROLE_ADMIN" : "ROLE_STAFF";
                                }
                            }

                            if (username != "UNKNOWN")
                            {
                                var claims = new List<Claim>
                                {
                                    new Claim(ClaimTypes.Name, username),
                                    new Claim(ClaimTypes.Role, role)
                                };

                                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                                var authProperties = new AuthenticationProperties { IsPersistent = true };

                                // Thực hiện đăng nhập vào Context hiện tại ngay lập tức
                                await context.SignInAsync(
                                    CookieAuthenticationDefaults.AuthenticationScheme,
                                    new ClaimsPrincipal(claimsIdentity),
                                    authProperties);
                                
                                // Quan trọng: Gán user vào context ngay để request hiện tại dùng được luôn
                                context.User = new ClaimsPrincipal(claimsIdentity);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Session validation error: " + ex.Message);
            }

            await _next(context);
        }
    }
}