using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using DuLich.Models.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace DuLich.Middlewares
{
    public class SessionValidationMiddleware
    {
        private readonly RequestDelegate _next;
        // [CẤU HÌNH] Thời gian Idle cho phép (phút)
        private const int IDLE_TIMEOUT_MINUTES = 2; 

        public SessionValidationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
        {
            try
            {
                var path = context.Request.Path.Value ?? string.Empty;

                // Bỏ qua file tĩnh và các trang login/register
                if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) && !path.Contains("/sessions")
                    || path.StartsWith("/Customer/Login", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("/Customer/Register", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("/css", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("/js", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("/images", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("/favicon.ico", StringComparison.OrdinalIgnoreCase))
                {
                    if (!path.StartsWith("/api/hoadon")) 
                    {
                        await _next(context);
                        return;
                    }
                }

                var sessionId = context.Request.Cookies["USER_SESSION_ID"];

                if (!string.IsNullOrEmpty(sessionId))
                {
                    var sess = await db.UserSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
                    
                    bool isValid = sess != null && sess.IsActive == "Y";

                    // Kiểm tra thời gian Idle
                    if (isValid)
                    {
                        var timeSinceLastActivity = DateTime.UtcNow - sess.LastActivity;
                        if (timeSinceLastActivity.TotalMinutes > IDLE_TIMEOUT_MINUTES)
                        {
                            // Quá 2 phút -> Hủy session
                            isValid = false;
                            sess.IsActive = "N"; 
                            db.UserSessions.Update(sess);
                            await db.SaveChangesAsync();
                            Console.WriteLine($"Session {sessionId} expired due to inactivity > {IDLE_TIMEOUT_MINUTES}m.");
                        }
                    }

                    if (!isValid)
                    {
                        // Logout và xóa Cookie
                        try
                        {
                            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        }
                        catch { }
                        context.Response.Cookies.Delete("USER_SESSION_ID");

                        if (!path.Contains("/Login"))
                        {
                            // Nếu là API thì trả về 401, nếu là trang Web thì redirect về Login
                            if (path.StartsWith("/api"))
                            {
                                context.Response.StatusCode = 401;
                                return;
                            }
                            else
                            {
                                context.Response.Redirect("/Customer/Login?reason=timeout");
                                return;
                            }
                        }
                    }
                    else
                    {
                        // Còn hạn -> Cập nhật lại thời gian hoạt động
                        sess.LastActivity = DateTime.UtcNow;
                        db.UserSessions.Update(sess);
                        await db.SaveChangesAsync();

                        // Logic Re-hydrate User (Tự động đăng nhập lại nếu Cookie mất nhưng Session DB còn)
                        if (context.User?.Identity?.IsAuthenticated != true && sess.UserId.HasValue)
                        {
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
                                if (sess.UserType != "CUSTOMER") // Add Branch Claim for Staff
                                {
                                     // Logic lấy chi nhánh nếu cần (đơn giản hóa ở đây)
                                }

                                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));
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