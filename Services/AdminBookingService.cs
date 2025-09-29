using BarBookingSystem.Data;
using BarBookingSystem.Models;
using BarBookingSystem.Models.ViewModels;
using BarBookingSystem.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BarBookingSystem.Services
{
    public class AdminBookingService : IAdminBookingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILineNotifyService _lineNotify;
        private readonly ILogger<AdminBookingService> _logger;

        public AdminBookingService(
            ApplicationDbContext context,
            ILineNotifyService lineNotify,
            ILogger<AdminBookingService> logger)
        {
            _context = context;
            _lineNotify = lineNotify;
            _logger = logger;
        }

        public async Task<List<Booking>> GetBookingsAsync(string status, DateTime? date, string search)
        {
            var query = _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Table).ThenInclude(t => t.Branch)
                .Include(b => b.Payment)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(b => b.Status == status);

            if (date.HasValue)
                query = query.Where(b => b.BookingDate == date.Value.Date);

            if (!string.IsNullOrEmpty(search))
                query = ApplySearchFilter(query, search);

            return await query
                .OrderByDescending(b => b.BookingDate)
                .ThenBy(b => b.StartTime)
                .Take(100)
                .ToListAsync();
        }

        public async Task<(bool Success, string Message)> ProcessCheckInAsync(string bookingCode)
        {
            try
            {
                if (string.IsNullOrEmpty(bookingCode))
                    return (false, "รหัสการจองไม่ถูกต้อง");

                var booking = await GetBookingForCheckInAsync(bookingCode.Trim());
                if (booking == null)
                    return (false, "ไม่พบการจองสำหรับรหัสนี้");

                var validation = ValidateCheckInTiming(booking);
                if (!validation.IsValid)
                    return (false, validation.Message);

                await ExecuteCheckInAsync(booking);
                await SendCheckInNotificationAsync(booking);

                _logger.LogInformation("CheckIn successful for booking {BookingCode}", bookingCode);
                return (true, $"Check-in สำเร็จสำหรับรหัส {bookingCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CheckIn error for booking code: {BookingCode}", bookingCode);
                return (false, "เกิดข้อผิดพลาดในระบบ");
            }
        }

        public async Task<bool> CancelBookingAsync(int bookingId)
        {
            var booking = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Table)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null || !CanCancelBooking(booking))
                return false;

            booking.Status = "Cancelled";
            booking.ModifiedAt = DateTime.UtcNow;

            _context.Update(booking);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<(bool Success, string Error)> UpdateBookingAsync(int id, Booking model)
        {
            try
            {
                var booking = await _context.Bookings
                    .Include(b => b.User)
                    .Include(b => b.Table)
                    .FirstOrDefaultAsync(b => b.Id == id);

                if (booking == null)
                    return (false, "ไม่พบการจองที่ต้องการแก้ไข");

                booking.UpdateFromModel(model);
                _context.Update(booking);
                await _context.SaveChangesAsync();

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating booking {BookingId}", id);
                return (false, "เกิดข้อผิดพลาดในการอัปเดตการจอง");
            }
        }

        private static IQueryable<Booking> ApplySearchFilter(IQueryable<Booking> query, string search)
        {
            return query.Where(b =>
                b.BookingCode.Contains(search) ||
                b.User.FullName.Contains(search) ||
                b.User.Email.Contains(search) ||
                b.User.PhoneNumber.Contains(search));
        }

        private async Task<Booking> GetBookingForCheckInAsync(string bookingCode)
        {
            return await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Table).ThenInclude(t => t.Branch)
                .FirstOrDefaultAsync(b => b.BookingCode == bookingCode);
        }

        private static (bool IsValid, string Message) ValidateCheckInTiming(Booking booking)
        {
            if (booking.Status != "Confirmed")
                return (false, $"สถานะการจองไม่ถูกต้อง: {booking.Status}");

            var nowThailand = DateTime.UtcNow.AddHours(7);
            var bookingDateTime = booking.BookingDate.Date.Add(booking.StartTime);
            var timeDiff = nowThailand - bookingDateTime;

            if (timeDiff.TotalHours < -1)
                return (false, $"ยังไม่ถึงเวลา Check-in\nเวลาปัจจุบัน: {nowThailand:dd/MM/yyyy HH:mm}\nเวลาจอง: {bookingDateTime:dd/MM/yyyy HH:mm}");

            if (timeDiff.TotalHours > 3)
                return (false, "เลยเวลา Check-in แล้ว");

            return (true, null);
        }

        private async Task ExecuteCheckInAsync(Booking booking)
        {
            booking.Status = "CheckedIn";
            booking.CheckInTime = DateTime.UtcNow;
            booking.ModifiedAt = DateTime.UtcNow;

            _context.Update(booking);
            await _context.SaveChangesAsync();
        }

        private async Task SendCheckInNotificationAsync(Booking booking)
        {
            try
            {
                if (_lineNotify != null)
                {
                    var tableInfo = booking?.Table?.TableNumber ?? "Unknown";
                    var branchInfo = booking?.Table?.Branch?.Name ?? "Unknown";
                    var message = $"Check-in สำเร็จ: {booking.BookingCode} - {booking.User.FullName} - โต๊ะ {tableInfo} - สาขา {branchInfo}";
                    // await _lineNotify.SendAdminNotificationAsync(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send check-in notification for booking {BookingCode}", booking.BookingCode);
            }
        }

        private static bool CanCancelBooking(Booking booking)
        {
            return booking.Status == "Pending" || booking.Status == "Confirmed";
        }
    }
}
