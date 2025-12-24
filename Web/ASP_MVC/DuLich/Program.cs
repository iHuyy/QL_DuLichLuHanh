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

// Register DbContext and Oracle session interceptor
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<DuLich.Models.Data.OracleSessionInterceptor>();
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    var interceptor = sp.GetRequiredService<OracleSessionInterceptor>();
    options.UseOracle(connectionString, o => o.CommandTimeout(5))
           .AddInterceptors(interceptor);
});
// Đăng ký Authentication (GIỮ NGUYÊN .AddCookie(), THÊM .AddJwtBearer())

// First, create the RSAService instance manually so it can be used during registration.
var rsaPrivateKeyPath = Path.Combine(builder.Environment.ContentRootPath, "Keys", "private_key_unencrypted.pem");
var rsaPublicKeyPath = Path.Combine(builder.Environment.ContentRootPath, "Keys", "public_key.pem");
var rsaServiceInstance = new RSAService(rsaPrivateKeyPath, rsaPublicKeyPath);

// Now, register the created instance as a singleton.
builder.Services.AddSingleton(rsaServiceInstance);

// Create and register DigitalSignatureService similarly.
var digitalSignatureServiceInstance = new DigitalSignatureService(rsaPrivateKeyPath, rsaPublicKeyPath);
builder.Services.AddSingleton(digitalSignatureServiceInstance);

// Configure authentication: Cookie as default, plus JWT for mobile clients
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
    .AddCookie(options =>
    {
        options.LoginPath = "/Admin/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(rsaServiceInstance.GetPublicKey())
        };
    });


// The RSAService is already registered as a singleton instance above.
// SSH service for backup/restore operations
builder.Services.AddSingleton<BackupSshService>();
// Service tracking restore state (used by middleware and controllers)
builder.Services.AddSingleton<RestoreStateService>();

// *** BẮT ĐẦU THÊM MỚI (SERVICES) ***
// Thêm dịch vụ tạo JWT
builder.Services.AddSingleton<JwtService>();

// Cache in-memory và dịch vụ email được dùng bởi OracleAuthService
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<EmailService>();

// Đăng ký OracleAuthService để các controller (Customer/Staff/API) có thể DI
builder.Services.AddScoped<OracleAuthService>();

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
