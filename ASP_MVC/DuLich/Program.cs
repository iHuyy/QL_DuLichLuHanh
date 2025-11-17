using DuLich.Models.Data;
using DuLich.Services;
using DuLich.Middlewares;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
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
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Customer/Login";
        options.AccessDeniedPath = "/Home/AccessDenied";
        // Make cookie policy developer-friendly: when running on HTTP (local dev), avoid SameSite=None without Secure
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
    })
    // *** BẮT ĐẦU THÊM MỚI (JWT) ***
    .AddJwtBearer(options => // Thêm cấu hình JWT Bearer cho API (Mobile app)
    {
        // Phải BuildServiceProvider tạm thời ở đây để lấy RSAService
        var sp = builder.Services.BuildServiceProvider(); 
        var rsaService = sp.GetRequiredService<RSAService>();
        var jwtSettings = builder.Configuration.GetSection("Jwt");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new RsaSecurityKey(rsaService.GetPublicKey()),
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
                Console.WriteLine(">>> TOKEN VALIDATED: " + context.Principal.Identity.Name);
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

// Đăng ký DbContext với interceptor
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    var interceptor = sp.GetRequiredService<OracleSessionInterceptor>();
    // Dùng connectionString đã khai báo ở trên
    options.UseOracle(connectionString) 
           .AddInterceptors(interceptor);
});

// Đăng ký OracleAuthService
builder.Services.AddScoped<OracleAuthService>();

// Register RSAService for signing invoices (keys stored under Keys)
builder.Services.AddSingleton<RSAService>(sp =>
{
    var privateKeyPath = Path.Combine(builder.Environment.ContentRootPath, "Keys", "private_key_unencrypted.pem");
    var publicKeyPath = Path.Combine(builder.Environment.ContentRootPath, "Keys", "public_key.pem");
    return new RSAService(privateKeyPath, publicKeyPath);
});
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
// *** KẾT THÚC THÊM MỚI (API ROUTE) ***

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Customer}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();