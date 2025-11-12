using DuLich.Models.Data;
using DuLich.Services;
using DuLich.Middlewares;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Đăng ký Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Customer/Login";
        options.AccessDeniedPath = "/Home/AccessDenied";
        // Make cookie policy developer-friendly: when running on HTTP (local dev), avoid SameSite=None without Secure
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
    });

// HttpContext accessor needed by DB connection interceptor
builder.Services.AddHttpContextAccessor();

// Register OracleSessionInterceptor (constructor will receive IHttpContextAccessor via DI)
builder.Services.AddScoped<OracleSessionInterceptor>();

// Đăng ký DbContext với interceptor
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    var interceptor = sp.GetRequiredService<OracleSessionInterceptor>();
    options.UseOracle(builder.Configuration.GetConnectionString("DefaultConnection"))
           .AddInterceptors(interceptor);
});

// Đăng ký OracleAuthService
builder.Services.AddScoped<OracleAuthService>();

// Register DigitalSignatureService
builder.Services.AddSingleton<DigitalSignatureService>(sp =>
{
    var privateKeyPath = Path.Combine(builder.Environment.ContentRootPath, "Keys", "private_key_unencrypted.pem");
    var publicKeyPath = Path.Combine(builder.Environment.ContentRootPath, "Keys", "public_key.pem");
    return new DigitalSignatureService(privateKeyPath, publicKeyPath);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
// Validate session right after authentication and before authorization
app.UseMiddleware<SessionValidationMiddleware>();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Customer}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();