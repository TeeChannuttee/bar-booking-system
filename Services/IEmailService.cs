using BarBookingSystem.Models;

namespace BarBookingSystem.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
        Task SendBookingConfirmationEmailAsync(Booking booking);
        Task SendPasswordResetEmailAsync(string email, string resetLink);
    }
}
