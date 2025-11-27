using System;
using System.Linq;
using System.Threading.Tasks;
using DuLich.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DuLich.Middlewares
{
    /// <summary>
    /// When a restore job is running, block other requests with a friendly message
    /// to avoid ORA-01109 while the database is offline.
    /// </summary>
    public class RestoreGateMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly RestoreStateService _state;
        private readonly ILogger<RestoreGateMiddleware> _logger;

        public RestoreGateMiddleware(RequestDelegate next, RestoreStateService state, ILogger<RestoreGateMiddleware> logger)
        {
            _next = next;
            _state = state;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!_state.IsRestoring)
            {
                await _next(context);
                return;
            }

            var path = context.Request.Path.Value ?? string.Empty;

            // Allow static files, SignalR, and the restore page itself to continue
            if (IsSafePath(path))
            {
                await _next(context);
                return;
            }

            _logger.LogWarning("Request blocked during restore: {Path}", path);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

            var accept = context.Request.Headers["Accept"].ToString();
            if (accept.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("""{"error":"restore_in_progress","message":"He thong dang phuc hoi, vui long thu lai sau."}""");
            }
            else
            {
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync(@"
<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""utf-8"">
    <title>Hệ thống đang phục hồi</title>
    <style>body{font-family:Arial, sans-serif;display:flex;align-items:center;justify-content:center;height:100vh;margin:0;background:#f8f9fa;color:#333;} .card{background:#fff;padding:24px 28px;border-radius:10px;box-shadow:0 4px 16px rgba(0,0,0,0.08);max-width:420px;text-align:center;} h1{font-size:22px;margin:0 0 10px;} p{margin:6px 0 0;} .small{font-size:13px;color:#666;}</style>
</head>
<body>
    <div class=""card"">
        <h1>Đang phục hồi dữ liệu</h1>
        <p>Hệ thống tạm thời không khả dụng trong vài phút. Vui lòng thử lại sau.</p>
        <p class=""small"">Bạn có thể mở lại trang ""Sao lưu &amp; Phục hồi"" để xem tiến độ.</p>
    </div>
</body>
</html>");
            }
        }

        private static bool IsSafePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var p = path.ToLowerInvariant();
            return p.StartsWith("/admin/backuprestore")
                   || p.StartsWith("/restorehub")
                   || p.StartsWith("/css")
                   || p.StartsWith("/js")
                   || p.StartsWith("/images")
                   || p.StartsWith("/lib")
                   || p.StartsWith("/favicon");
        }
    }
}
