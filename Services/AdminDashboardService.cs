using BarBookingSystem.Data;
using BarBookingSystem.Models;
using BarBookingSystem.Models.ViewModels;
using BarBookingSystem.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BarBookingSystem.Services
{
    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminDashboardService> _logger;

        public AdminDashboardService(ApplicationDbContext context, ILogger<AdminDashboardService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<AdminDashboardViewModel> GetDashboardDataAsync()
        {
            var nowUtc = DateTime.UtcNow;
            var todayStartUtc = nowUtc.GetDayStart();
            var tomorrowStartUtc = todayStartUtc.AddDays(1);
            var weekStartUtc = todayStartUtc.GetWeekStart();

            var stats = await CalculateStatsAsync(todayStartUtc, tomorrowStartUtc, weekStartUtc);
            var charts = await GenerateChartsAsync(weekStartUtc, todayStartUtc);
            var bookings = await GetBookingListsAsync(todayStartUtc, tomorrowStartUtc, nowUtc);

            return new AdminDashboardViewModel
            {
                // Today stats
                TodayBookings = stats.TodayBookings,
                TodayCheckIns = stats.TodayCheckIns,
                TodayRevenue = stats.TodayRevenue,
                TodayNewCustomers = stats.TodayNewCustomers,
                TodayPendingCheckIns = stats.TodayPendingCheckIns,

                // Week stats
                WeekBookings = stats.WeekBookings,
                WeekRevenue = stats.WeekRevenue,
                WeekCancellations = stats.WeekCancellations,
                WeekNewCustomers = stats.WeekNewCustomers,

                // Performance metrics
                AverageRevenuePerBooking = stats.WeekBookings > 0 ? stats.WeekRevenue / stats.WeekBookings : 0m,
                BookingGrowthRate = await CalculateGrowthRateAsync(weekStartUtc, stats.WeekBookings),
                CancellationRate = CalculateCancellationRate(stats.WeekBookings, stats.WeekCancellations),
                AverageSpend = stats.WeekBookings > 0 ? stats.WeekRevenue / stats.WeekBookings : 0m,

                // Lists and charts
                UpcomingBookings = bookings.Upcoming,
                RecentBookings = bookings.Recent,
                RevenueChart = charts.RevenueChart,
                BookingsByZone = charts.BookingsByZone
            };
        }

        private async Task<DashboardStats> CalculateStatsAsync(DateTime todayStart, DateTime tomorrowStart, DateTime weekStart)
        {
            // รัน query ทีละตัวตามลำดับ เพื่อหลีกเลี่ยง concurrent operation บน DbContext
            var todayBookings = await _context.Bookings
                .CountAsync(b => b.BookingDate >= todayStart && b.BookingDate < tomorrowStart && b.Status == "Confirmed");

            var todayCheckIns = await _context.Bookings
                .CountAsync(b => b.CheckInTime >= todayStart && b.CheckInTime < tomorrowStart);

            var todayPendingCheckIns = await _context.Bookings
                .CountAsync(b => b.BookingDate >= todayStart && b.BookingDate < tomorrowStart
                    && b.Status == "Confirmed" && b.CheckInTime == null);

            var todayNewCustomers = await _context.Users
                .CountAsync(u => u.CreatedAt >= todayStart && u.CreatedAt < tomorrowStart);

            var weekBookings = await _context.Bookings
                .CountAsync(b => b.BookingDate >= weekStart && b.Status == "Confirmed");

            var weekCancellations = await _context.Bookings
                .CountAsync(b => b.ModifiedAt >= weekStart && b.Status == "Cancelled");

            var weekNewCustomers = await _context.Users
                .CountAsync(u => u.CreatedAt >= weekStart);

            var todayRevenue = await _context.Payments
                .Where(p => p.PaymentDate >= todayStart && p.PaymentDate < tomorrowStart && p.Status == "Completed")
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            var weekRevenue = await _context.Payments
                .Where(p => p.PaymentDate >= weekStart && p.Status == "Completed")
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            return new DashboardStats
            {
                TodayBookings = todayBookings,
                TodayCheckIns = todayCheckIns,
                TodayPendingCheckIns = todayPendingCheckIns,
                TodayNewCustomers = todayNewCustomers,
                WeekBookings = weekBookings,
                WeekCancellations = weekCancellations,
                WeekNewCustomers = weekNewCustomers,
                TodayRevenue = todayRevenue,
                WeekRevenue = weekRevenue
            };
        }

        private async Task<DashboardCharts> GenerateChartsAsync(DateTime weekStart, DateTime todayStart)
        {
            // Revenue chart (7 days)
            var revenueDaily = await _context.Payments
                .Where(p => p.PaymentDate >= weekStart && p.Status == "Completed")
                .GroupBy(p => p.PaymentDate.Date)
                .Select(g => new { Day = g.Key, Total = g.Sum(p => p.Amount) })
                .ToListAsync();

            var revenueChart = Enumerable.Range(0, 7)
                .Select(i =>
                {
                    var date = todayStart.AddDays(-6 + i).Date;
                    var revenue = revenueDaily.FirstOrDefault(x => x.Day.Date == date)?.Total ?? 0m;
                    return new DashboardPoint { Label = date.ToString("dd/MM"), Value = revenue };
                }).ToList();

            // Bookings by zone
            var bookingsByZone = await _context.Bookings
                .Include(b => b.Table)
                .Where(b => b.BookingDate >= weekStart && b.Status != "Cancelled")
                .GroupBy(b => b.Table.Zone ?? "ไม่ระบุ")
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            return new DashboardCharts
            {
                RevenueChart = revenueChart,
                BookingsByZone = bookingsByZone
            };
        }

        private async Task<DashboardBookings> GetBookingListsAsync(DateTime todayStart, DateTime tomorrowStart, DateTime nowUtc)
        {
            // รัน query ทีละตัวแทนการใช้ Task แยก
            var upcoming = await _context.Bookings
                .Include(b => b.User).Include(b => b.Table).ThenInclude(t => t.Branch)
                .Where(b => b.BookingDate >= todayStart && b.Status == "Confirmed")
                .OrderBy(b => b.BookingDate).ThenBy(b => b.StartTime)
                .Take(10).ToListAsync();

            var recent = await _context.Bookings
                .Include(b => b.User).Include(b => b.Table)
                .Where(b => b.CreatedAt >= nowUtc.AddHours(-24))
                .OrderByDescending(b => b.CreatedAt)
                .Take(10).ToListAsync();

            return new DashboardBookings
            {
                Upcoming = upcoming,
                Recent = recent
            };
        }

        private async Task<double> CalculateGrowthRateAsync(DateTime weekStart, int currentWeekBookings)
        {
            var lastWeekStart = weekStart.AddDays(-7);
            var lastWeekBookings = await _context.Bookings
                .CountAsync(b => b.BookingDate >= lastWeekStart && b.BookingDate < weekStart && b.Status == "Confirmed");

            return lastWeekBookings > 0 ? ((double)(currentWeekBookings - lastWeekBookings) / lastWeekBookings) * 100.0 : 0.0;
        }

        private static double CalculateCancellationRate(int weekBookings, int weekCancellations)
        {
            var total = weekBookings + weekCancellations;
            return total > 0 ? (double)weekCancellations / total * 100.0 : 0.0;
        }
    }
}