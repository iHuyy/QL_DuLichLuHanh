using DuLich.Models.Data;
using DuLich.Services;
using DuLich.Middlewares;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using DuLich.BackgroundServices;
using DuLich.Hubs;
// *** BẮT ĐẦU THÊM MỚI (USING) ***
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using Oracle.ManagedDataAccess.Client;
// *** KẾT THÚC THÊM MỚI (USING) ***

var builder = WebApplication.CreateBuilder(args);

// *** BẮT ĐẦU THÊM MỚI (ORACLE CONFIG) ***
// Cấu hình Oracle
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
OracleConfiguration.TnsAdmin = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Wallet_ORCLPDB");
OracleConfiguration.WalletLocation = OracleConfiguration.TnsAdmin;
// *** KẾT THÚC THÊM MỚI (ORACLE CONFIG) ***

// Add services to the container.
builder.Services.AddControllersWithViews();

// Đăng ký Authentication (GIỮ NGUYÊN .AddCookie(), THÊM .AddJwtBearer())

// First, create the RSAService instance manually so it can be used during registration.
var rsaPrivateKeyPath = Path.Combine(builder.Environment.ContentRootPath, "Keys", "private_key_unencrypted.pem");
var rsaPublicKeyPath = Path.Combine(builder.Environment.ContentRootPath, "Keys", "public_key.pem");
var rsaServiceInstance = new RSAService(rsaPrivateKeyPath, rsaPublicKeyPath);

// Now, register the created instance as a singleton.
builder.Services.AddSingleton(rsaServiceInstance);


builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Customer/Login";
        options.AccessDeniedPath = "/Home/AccessDenied";
        // Make cookie policy developer-friendly: when running on HTTP (local dev), avoid SameSite=None without Secure
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
        // For AJAX/API calls we should return 401/403 instead of redirecting to the login page
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = ctx =>
            {
                var path = ctx.Request.Path.Value ?? string.Empty;
                if (path.StartsWith("/staff/api", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }
                ctx.Response.Redirect(ctx.RedirectUri);
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = ctx =>
            {
                var path = ctx.Request.Path.Value ?? string.Empty;
                if (path.StartsWith("/staff/api", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }
                ctx.Response.Redirect(ctx.RedirectUri);
                return Task.CompletedTask;
            }
        };
    })
    // *** BẮT ĐẦU THÊM MỚI (JWT) ***
    .AddJwtBearer(options => // Thêm cấu hình JWT Bearer cho API (Mobile app)
    {
        var jwtSettings = builder.Configuration.GetSection("Jwt");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new RsaSecurityKey(rsaServiceInstance.GetPublicKey()),
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine(">>> AUTH FAILED: " + context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine(">>> TOKEN VALIDATED: " + context.Principal?.Identity?.Name);
                return Task.CompletedTask;
            },
            OnMessageReceived = context =>
            {
                Console.WriteLine(">>> TOKEN RECEIVED: " + context.Token);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Console.WriteLine(">>> AUTH CHALLENGE (401): " + context.Error + " - " + context.ErrorDescription);
                return Task.CompletedTask;
            }
        };
    });
// *** KẾT THÚC THÊM MỚI (JWT) ***

// HttpContext accessor needed by DB connection interceptor
builder.Services.AddHttpContextAccessor();

// Register OracleSessionInterceptor (constructor will receive IHttpContextAccessor via DI)
builder.Services.AddScoped<OracleSessionInterceptor>();

builder.Services.AddSingleton<RestoreStateService>();

builder.Services.AddTransient<DuLich.Services.EmailService>();
// Đảm bảo MemoryCache đã được thêm (thường mặc định có trong MVC, nếu chưa thì thêm:)
builder.Services.AddMemoryCache();

// Đăng ký DbContext với interceptor
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    var interceptor = sp.GetRequiredService<OracleSessionInterceptor>();
    // Dùng connectionString đã khai báo ở trên
    options.UseOracle(connectionString, o => o.CommandTimeout(5))
           .AddInterceptors(interceptor);
});

// Đăng ký OracleAuthService
builder.Services.AddScoped<OracleAuthService>();

// The RSAService is already registered as a singleton instance above.
// SSH service for backup/restore operations
builder.Services.AddSingleton<BackupSshService>();

// *** BẮT ĐẦU THÊM MỚI (SERVICES) ***
// Thêm dịch vụ tạo JWT
builder.Services.AddSingleton<JwtService>();

// Thêm dịch vụ Session (cần cho SessionValidationMiddleware)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Thêm Authorization Policy cho Mobile

builder.Services.AddAuthorization(options =>

{

    // Policy này yêu cầu một JWT hợp lệ (cho Mobile)

    options.AddPolicy("MobileUser", policy =>

    {

        policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);

        policy.RequireAuthenticatedUser();

        policy.RequireRole("ROLE_CUSTOMER", "ROLE_STAFF", "ROLE_ADMIN");

    });



    // [Authorize] (không có policy) sẽ tự động dùng Scheme mặc định (Cookie)

});



// *** BẮT ĐẦU THÊM MỚI (SIGNALR & BACKGROUND TASKS) ***

builder.Services.AddSignalR();

builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();

builder.Services.AddHostedService<QueuedHostedService>();

// Add the new service to automatically update expired tours
builder.Services.AddHostedService<TourStatusUpdaterService>();
builder.Services.AddHostedService<BackupSchedulerService>();

// *** KẾT THÚC THÊM MỚI (SIGNALR & BACKGROUND TASKS) ***



// *** KẾT THÚC THÊM MỚI (SERVICES) ***





var app = builder.Build();



// Configure the HTTP request pipeline.

if (!app.Environment.IsDevelopment())

{

    app.UseExceptionHandler("/Home/Error");

    app.UseHsts();

}



// *** SỬA LỖI: Comment dòng này lại ***

// app.UseHttpsRedirection(); 

// *** LÝ DO: Để cho phép Mobile gọi vào HTTP port 5127 mà không bị redirect sang HTTPS (gây lỗi 405)



app.UseStaticFiles();

app.UseRouting();

app.UseMiddleware<RestoreGateMiddleware>();



// *** BẮT ĐẦU THÊM MỚI (SESSION) ***

app.UseSession(); // Phải gọi UseSession() trước UseMiddleware

// *** KẾT THÚC THÊM MỚI (SESSION) ***



app.UseAuthentication();

// Validate session right after authentication and before authorization

app.UseMiddleware<SessionValidationMiddleware>();

app.UseAuthorization();



app.MapStaticAssets();



// *** BẮT ĐẦU THÊM MỚI (API ROUTE) ***

// Thêm route cho các API Controller (QrLoginController, ApiAuthController)

app.MapControllers();



// Map the SignalR hub

app.MapHub<RestoreHub>("/restoreHub");

// *** KẾT THÚC THÊM MỚI (API ROUTE) ***



app.MapControllerRoute(

    name: "default",

    pattern: "{controller=Customer}/{action=Index}/{id?}")

    .WithStaticAssets();



app.Run();
