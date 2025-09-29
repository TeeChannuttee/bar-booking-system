using BarBookingSystem.Models;

namespace BarBookingSystem.Services
{
    public interface ILineNotifyService
    {
        Task SendMessageAsync(string message);
        Task SendBookingConfirmationAsync(Booking booking);
        Task SendBookingReminderAsync(Booking booking);
        Task SendBookingCancellationAsync(Booking booking);
        Task SendAdminNotificationAsync(string message);
    }
}