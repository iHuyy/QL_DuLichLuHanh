using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using DuLich.Models.Data;
using Microsoft.EntityFrameworkCore;
using System;

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

                // Skip validation for static assets and auth endpoints
                if (path.StartsWith("/Customer/Login", StringComparison.OrdinalIgnoreCase)
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
                Console.WriteLine($"SessionValidationMiddleware: incoming path={path}, USER_SESSION_ID={(sessionId ?? "<none>")}");

                if (!string.IsNullOrEmpty(sessionId))
                {
                    var sess = await db.UserSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
                    Console.WriteLine(sess == null ? "Session not found in DB" : $"Session found: UserId={sess.UserId}, IsActive={sess.IsActive}");

                    if (sess == null || sess.IsActive != "Y")
                    {
                        // Invalid session -> sign out cookie auth, delete session cookie and redirect to login
                        try
                        {
                            await context.SignOutAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
                        }
                        catch { }
                        context.Response.Cookies.Delete("USER_SESSION_ID");
                        context.Response.Redirect("/Customer/Login");
                        return;
                    }

                    // update last activity
                    sess.LastActivity = DateTime.UtcNow;
                    db.UserSessions.Update(sess);
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                // log and continue to next middleware
                Console.WriteLine("Session validation error: " + ex.Message);
            }

            await _next(context);
        }
    }
}
