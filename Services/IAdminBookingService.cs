using BarBookingSystem.Models;

namespace BarBookingSystem.Services
{
    public interface IAdminBookingService
    {
        Task<List<Booking>> GetBookingsAsync(string status, DateTime? date, string search);
        Task<(bool Success, string Message)> ProcessCheckInAsync(string bookingCode);
        Task<bool> CancelBookingAsync(int bookingId);
        Task<(bool Success, string Error)> UpdateBookingAsync(int id, Booking model);
    }
}