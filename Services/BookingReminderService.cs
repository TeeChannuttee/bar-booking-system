using BarBookingSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace BarBookingSystem.Services
{
    public class BookingReminderService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BookingReminderService> _logger;

        public BookingReminderService(
            IServiceProvider serviceProvider,
            ILogger<BookingReminderService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var lineNotify = scope.ServiceProvider.GetRequiredService<ILineNotifyService>();

                    // Send reminders for bookings in next 2 hours
                    var reminderTime = DateTime.Now.AddHours(2);
                    var bookings = await context.Bookings
                        .Include(b => b.User)
                        .Include(b => b.Table)
                            .ThenInclude(t => t.Branch)
                        .Where(b => b.Status == "Confirmed"
                            && b.BookingDate.Date == reminderTime.Date
                            && b.StartTime.Hours == reminderTime.Hour
                            && !b.ReminderSent)
                        .ToListAsync(stoppingToken);

                    foreach (var booking in bookings)
                    {
                        await lineNotify.SendBookingReminderAsync(booking);
                        booking.ReminderSent = true;
                    }

                    if (bookings.Any())
                    {
                        await context.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation($"Sent {bookings.Count} booking reminders");
                    }

                    // Check for no-shows (30 minutes after start time)
                    var noShowTime = DateTime.Now.AddMinutes(-30);
                    var noShowBookings = await context.Bookings
                        .Where(b => b.Status == "Confirmed"
                            && b.BookingDate.Date == noShowTime.Date
                            && b.StartTime.Hours == noShowTime.Hour
                            && b.StartTime.Minutes == noShowTime.Minute)
                        .ToListAsync(stoppingToken);

                    foreach (var booking in noShowBookings)
                    {
                        booking.Status = "NoShow";
                        await lineNotify.SendAdminNotificationAsync(
                            $"No Show: {booking.BookingCode} - Table {booking.Table.TableNumber}");
                    }

                    if (noShowBookings.Any())
                    {
                        await context.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation($"Marked {noShowBookings.Count} bookings as no-show");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in BookingReminderService");
                }

                // Run every 30 minutes
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }
    }
}

