using BarBookingSystem.Models;
using BarBookingSystem.Models.ViewModels;

namespace BarBookingSystem.Services
{
    public interface IBookingService
    {
        Task<List<Table>> GetAvailableTablesAsync(
            int branchId, DateTime date, TimeSpan startTime, int duration, int guests, string? zone = null);

        Task<Booking> CreateBookingAsync(CreateBookingViewModel model, string userId);

        // เช็คพื้นฐาน: Active + ช่วงวันที่ + MaxUses
        Task<PromoCode?> ValidatePromoCodeAsync(string code);

        Task<string> GenerateBookingCodeAsync();
        Task<bool> CancelBookingAsync(int bookingId, string userId);
        Task<bool> CheckInAsync(string bookingCode);
    }

}
