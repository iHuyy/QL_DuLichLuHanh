using DuLich.Models.Data;
using DuLich.Services;
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
    });

// HttpContext accessor needed by DB connection interceptor
builder.Services.AddHttpContextAccessor();

// Register interceptor
builder.Services.AddScoped<OracleClientIdentifierInterceptor>();

// Đăng ký DbContext
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    options.UseOracle(builder.Configuration.GetConnectionString("DefaultConnection"));
    // Add interceptor resolved from DI to set CLIENT_IDENTIFIER for Oracle sessions
    var interceptor = sp.GetService<OracleClientIdentifierInterceptor>();
    if (interceptor != null) options.AddInterceptors(interceptor);
});

// Đăng ký OracleAuthService
builder.Services.AddScoped<OracleAuthService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Customer}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
