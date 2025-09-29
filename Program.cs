using BarBookingSystem.Data;
using BarBookingSystem.Hubs;
using BarBookingSystem.Models;
using BarBookingSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// === DATABASE CONFIGURATION (SUPABASE) ===
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null
            );
        }
    ));

// === IDENTITY CONFIGURATION ===
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = true;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false; // Set true in production
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// === COOKIE CONFIGURATION ===
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

// === REGISTER SERVICES ===
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ILineNotifyService, LineNotifyService>();
builder.Services.AddScoped<IBookingService, BookingService>();
//builder.Services.AddScoped<IPaymentService, StripePaymentService>();

// Background Service for Reminders
builder.Services.AddHostedService<BookingReminderService>();
builder.Services.AddScoped<IAccountService, AccountService>();

// Service Registration
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IAdminTableService, AdminTableService>();
builder.Services.AddScoped<IAdminPromoService, AdminPromoService>();
builder.Services.AddScoped<IAdminReportService, AdminReportService>();
// Service Registration (เพิ่มอีก 2 ตัวที่ยังไม่มี)
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddScoped<IAdminBookingService, AdminBookingService>();

// === MVC & API CONFIGURATION ===
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

// Add API Controllers
builder.Services.AddEndpointsApiExplorer();

// === SIGNALR FOR REAL-TIME UPDATES ===
builder.Services.AddSignalR();

// === SESSION CONFIGURATION ===
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// === CORS CONFIGURATION ===
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});



var app = builder.Build();

// ต้องมาก่อน HTTPS/CORS/Auth เพื่อให้ header จาก proxy ทำงานถูก
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});


// === CONFIGURE PIPELINE ===
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

// === MAP ROUTES ===
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<BookingHub>("/bookingHub");

// === INITIALIZE DATABASE & SEED DATA ===
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        // Apply migrations
        await context.Database.MigrateAsync();

        // Seed data
        await DataSeeder.SeedAsync(context, userManager, roleManager);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

app.Run();