using BookStore.Business;
using BookStore.Business.Services;
using BookStore.Data;
using BookStore.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// ---------------- Dữ liệu + nghiệp vụ dùng chung với ứng dụng Desktop ----------------
var connectionString = builder.Configuration.GetConnectionString("BookStore")
    ?? throw new InvalidOperationException("Thiếu ConnectionStrings:BookStore trong appsettings.json");
builder.Services.AddBookStore(connectionString);

// ---------------- MVC + chống giả mạo request (CSRF) cho mọi form POST ----------------
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

// Giỏ hàng của khách chưa đăng nhập được giữ trong Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromDays(2);
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
});

// ---------------- Đăng nhập bằng Cookie ----------------
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.AccessDeniedPath = "/Account/Login";
        o.ExpireTimeSpan = TimeSpan.FromDays(7);
        o.SlidingExpiration = true;
        o.Events.OnValidatePrincipal = async context =>
        {
            // Nếu nhân viên khóa tài khoản trên Desktop, khách bị đăng xuất ngay ở request kế tiếp
            var idClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var customers = context.HttpContext.RequestServices.GetRequiredService<CustomerService>();
            var profile = int.TryParse(idClaim, out var id) ? await customers.GetProfileAsync(id) : null;
            if (profile is not { IsActive: true })
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });
builder.Services.AddAuthorization(o =>
    o.AddPolicy("Customer", p => p.RequireRole(UserRole.Customer.ToString())));

var app = builder.Build();

// Tạo database (Migrations) + dữ liệu mẫu nếu chưa có
await DatabaseInitializer.InitializeAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/Home/NotFound", "?code={0}");

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
