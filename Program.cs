using BarBookingSystem.Data;
using BarBookingSystem.Hubs;
using BarBookingSystem.Models;
using BarBookingSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// ====== PORT from Railway ======
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// ====== Feature flags ======
bool autoMigrate = builder.Configuration.GetValue("AUTO_MIGRATE", false);
bool jobsEnabled = builder.Configuration.GetValue("JOBS__ENABLED", false);

// ====== DB (Supabase PgBouncer: port 6543) ======
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql =>
        {
            npgsql.CommandTimeout(30);
            npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
        }
    ));

// ====== Identity ======
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = true;

    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;

    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false; // ตั้ง true เมื่อ production พร้อม
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ====== Cookie (สำคัญสำหรับ Stripe redirect) ======
builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.LogoutPath = "/Account/Logout";
    o.AccessDeniedPath = "/Account/AccessDenied";
    o.ExpireTimeSpan = TimeSpan.FromDays(7);
    o.SlidingExpiration = true;

    // ✅ ต้องเปิดให้ส่ง cookie ตอน redirect จาก Stripe
    o.Cookie.SameSite = SameSiteMode.None;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// ====== Session Cookie ======
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;

    // ✅ ปรับ SameSite/HTTPS เช่นเดียวกัน
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// ====== Antiforgery Cookie (ใช้ในฟอร์มที่มี [ValidateAntiForgeryToken]) ======
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// ====== Services ======
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ILineNotifyService, LineNotifyService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IAdminTableService, AdminTableService>();
builder.Services.AddScoped<IAdminPromoService, AdminPromoService>();
builder.Services.AddScoped<IAdminReportService, AdminReportService>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddScoped<IAdminBookingService, AdminBookingService>();

// Background jobs
if (jobsEnabled)
{
    builder.Services.AddHostedService<BookingReminderService>();
}

// ====== MVC / API / SignalR ======
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// ====== Forwarded headers (ต้องอยู่ข้างบนสุด และเรียกครั้งเดียว) ======
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedHost,
    KnownNetworks = { new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("100.64.0.0"), 10) }


// ====== Error/HSTS ======
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// ====== HTTPS / Static / Routing ======
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ====== Session / CORS / Auth ======
app.UseSession();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

// ====== Routes ======
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// SignalR
app.MapHub<BookingHub>("/bookingHub");

// ====== Health ======
app.MapGet("/healthz", () => Results.Ok("OK"));
app.MapGet("/dbping", async (IConfiguration cfg) =>
{
    var cs = cfg.GetConnectionString("DefaultConnection");
    await using var conn = new NpgsqlConnection(cs);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand("SELECT 1", conn);
    var val = await cmd.ExecuteScalarAsync();
    return Results.Ok(new { db = "ok", val });
});

// ====== Auto-Migrate ======
if (autoMigrate)
{
    _ = Task.Run(async () =>
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();
        try
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

            await context.Database.MigrateAsync();
            await DataSeeder.SeedAsync(context, userManager, roleManager);

            logger.LogInformation("Migration/Seed completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Migration/Seed failed");
        }
    });
}

app.Run();
