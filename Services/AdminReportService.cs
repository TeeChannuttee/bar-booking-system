using BarBookingSystem.Data;
using BarBookingSystem.Models;
using BarBookingSystem.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace BarBookingSystem.Services
{
    public class AdminReportService : IAdminReportService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminReportService> _logger;

        public AdminReportService(ApplicationDbContext context, ILogger<AdminReportService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ReportsViewModel> GenerateReportsAsync(DateTime startDate, DateTime endDate)
        {
            // Convert to UTC covering full days
            var startDateUtc = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            var endDateUtc = DateTime.SpecifyKind(endDate.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

            // Get all bookings in date range
            var bookingsQuery = _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Table).ThenInclude(t => t.Branch)
                .Include(b => b.Payment)
                .Where(b => b.BookingDate >= startDateUtc && b.BookingDate <= endDateUtc);

            var allBookings = await bookingsQuery.ToListAsync();
            var confirmedBookings = allBookings.Where(b => b.Status != "Cancelled").ToList();
            var cancelledBookings = allBookings.Where(b => b.Status == "Cancelled").ToList();

            // Calculate summary statistics
            var totalRevenue = await CalculateTotalRevenueAsync(startDateUtc, endDateUtc);
            var totalBookings = confirmedBookings.Count;
            var cancellationRate = allBookings.Count > 0
                ? (double)cancelledBookings.Count / allBookings.Count * 100.0
                : 0.0;

            // Generate charts
            var (chartLabels, chartData) = await GenerateRevenueChartAsync(startDateUtc, endDateUtc);
            var (zoneLabels, zoneData) = GenerateZoneChartAsync(confirmedBookings);

            // Generate top lists
            var topTables = GenerateTopTablesAsync(confirmedBookings);
            var topCustomers = GenerateTopCustomersAsync(confirmedBookings);

            return new ReportsViewModel
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalRevenue = totalRevenue,
                TotalBookings = totalBookings,
                CancellationRate = cancellationRate,
                ChartLabels = chartLabels,
                ChartData = chartData,
                ZoneLabels = zoneLabels,
                ZoneData = zoneData,
                TopTables = topTables,
                TopCustomers = topCustomers
            };
        }

        public async Task<byte[]> ExportBookingsToCsvAsync(DateTime? startDate, DateTime? endDate)
        {
            var query = _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Table).ThenInclude(t => t.Branch)
                .Include(b => b.Payment)
                .AsQueryable();

            if (startDate.HasValue)
                query = query.Where(b => b.BookingDate >= startDate);

            if (endDate.HasValue)
                query = query.Where(b => b.BookingDate <= endDate);

            var bookings = await query.ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("BookingCode,Date,Time,Customer,Phone,Table,Zone,Guests,Amount,Status");

            foreach (var booking in bookings)
            {
                csv.AppendLine($"{booking.BookingCode}," +
                    $"{booking.BookingDate:yyyy-MM-dd}," +
                    $"{booking.StartTime}-{booking.EndTime}," +
                    $"{booking.User.FullName}," +
                    $"{booking.User.PhoneNumber}," +
                    $"{booking.Table.TableNumber}," +
                    $"{booking.Table.Zone}," +
                    $"{booking.NumberOfGuests}," +
                    $"{booking.TotalAmount}," +
                    $"{booking.Status}");
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        private async Task<decimal> CalculateTotalRevenueAsync(DateTime startDateUtc, DateTime endDateUtc)
        {
            return await _context.Payments
                .Where(p => p.PaymentDate >= startDateUtc
                         && p.PaymentDate <= endDateUtc
                         && p.Status == "Completed")
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;
        }

        private async Task<(List<string> labels, List<decimal> data)> GenerateRevenueChartAsync(DateTime startDateUtc, DateTime endDateUtc)
        {
            var revenueByDate = await _context.Payments
                .Where(p => p.PaymentDate >= startDateUtc
                         && p.PaymentDate <= endDateUtc
                         && p.Status == "Completed")
                .GroupBy(p => p.PaymentDate.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(p => p.Amount) })
                .OrderBy(x => x.Date)
                .ToListAsync();

            var labels = new List<string>();
            var data = new List<decimal>();

            // Generate data for all days in range
            for (var date = startDateUtc.Date; date <= endDateUtc.Date; date = date.AddDays(1))
            {
                labels.Add(date.ToString("dd/MM"));
                var dayRevenue = revenueByDate.FirstOrDefault(x => x.Date.Date == date.Date)?.Total ?? 0m;
                data.Add(dayRevenue);
            }

            return (labels, data);
        }

        private static (List<string> labels, List<int> data) GenerateZoneChartAsync(List<Booking> confirmedBookings)
        {
            var zoneBookings = confirmedBookings
                .GroupBy(b => b.Table.Zone ?? "ไม่ระบุ")
                .Select(g => new { Zone = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            var labels = zoneBookings.Select(x => x.Zone).ToList();
            var data = zoneBookings.Select(x => x.Count).ToList();

            return (labels, data);
        }

        private static List<TopTableDto> GenerateTopTablesAsync(List<Booking> confirmedBookings)
        {
            return confirmedBookings
                .GroupBy(b => new { b.Table.TableNumber, b.Table.Branch.Name })
                .Select(g => new TopTableDto
                {
                    TableNumber = g.Key.TableNumber,
                    BranchName = g.Key.Name,
                    BookingCount = g.Count()
                })
                .OrderByDescending(t => t.BookingCount)
                .Take(5)
                .Select((t, index) => new TopTableDto
                {
                    Rank = index + 1,
                    TableNumber = t.TableNumber,
                    BranchName = t.BranchName,
                    BookingCount = t.BookingCount
                })
                .ToList();
        }

        private static List<TopCustomerDto> GenerateTopCustomersAsync(List<Booking> confirmedBookings)
        {
            return confirmedBookings
                .GroupBy(b => b.User)
                .Select(g => new TopCustomerDto
                {
                    Name = g.Key.FullName ?? g.Key.UserName ?? "ไม่ระบุ",
                    BookingCount = g.Count(),
                    TotalSpent = g.Sum(b => b.TotalAmount)
                })
                .OrderByDescending(c => c.TotalSpent)
                .Take(5)
                .Select((c, index) => new TopCustomerDto
                {
                    Rank = index + 1,
                    Name = c.Name,
                    BookingCount = c.BookingCount,
                    TotalSpent = c.TotalSpent
                })
                .ToList();
        }
    }
}