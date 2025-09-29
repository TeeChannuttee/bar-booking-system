using BarBookingSystem.Data;
using BarBookingSystem.Models;
using BarBookingSystem.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BarBookingSystem.Services
{
    public class BookingService : IBookingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILineNotifyService _lineNotify;
        private readonly IEmailService _emailService;
        private readonly ILogger<BookingService> _logger;

        public BookingService(
            ApplicationDbContext context,
            ILineNotifyService lineNotify,
            IEmailService emailService,
            ILogger<BookingService> logger)
        {
            _context = context;
            _lineNotify = lineNotify;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<List<Table>> GetAvailableTablesAsync(
            int branchId, DateTime date, TimeSpan startTime, int duration, int guests, string? zone = null)
        {
            // ✅ บังคับให้เป็น UTC สำหรับ PostgreSQL
            var dateUtc = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            var dayStart = dateUtc;
            var dayEnd = dayStart.AddDays(1);

            var start = startTime;
            var end = start.Add(TimeSpan.FromHours(duration));

            var query = _context.Tables
                .Where(t => t.BranchId == branchId && t.IsActive && t.Capacity >= guests);

            if (!string.IsNullOrWhiteSpace(zone))
                query = query.Where(t => t.Zone == zone);

            var tables = await query
                .Where(t => !_context.Bookings.Any(b =>
                    b.TableId == t.Id
                    && b.Status != "Cancelled"
                    && b.BookingDate >= dayStart && b.BookingDate < dayEnd
                    && b.StartTime < end
                    && b.EndTime > start
                ))
                .OrderBy(t => t.Zone).ThenBy(t => t.TableNumber)
                .ToListAsync();

            return tables;
        }

        public async Task<Booking> CreateBookingAsync(CreateBookingViewModel model, string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var table = await _context.Tables
                    .Include(t => t.Branch)
                    .FirstOrDefaultAsync(t => t.Id == model.TableId.Value);

                if (table == null)
                    throw new InvalidOperationException("Table not found");

                // ✅ บังคับให้ BookingDate เป็น UTC
                var bookingDateUtc = DateTime.SpecifyKind(model.BookingDate.Date, DateTimeKind.Utc);

                var booking = new Booking
                {
                    BookingCode = await GenerateBookingCodeAsync(),
                    UserId = userId,
                    TableId = model.TableId.Value,
                    BookingDate = bookingDateUtc, // ✅ ใช้ UTC date
                    StartTime = TimeSpan.Parse(model.StartTime),
                    EndTime = TimeSpan.Parse(model.StartTime).Add(TimeSpan.FromHours(model.Duration)),
                    NumberOfGuests = model.NumberOfGuests,
                    Status = "Pending",
                    SpecialRequests = model.SpecialRequests,
                    CreatedAt = DateTime.UtcNow,
                    QRCodeData = Guid.NewGuid().ToString(),
                    Table = table
                };

                // คำนวณยอดรวมพื้นฐาน
                decimal totalBase = table.MinimumSpend + table.BasePrice;
                decimal discountAmount = 0m;
                string? appliedPromoCode = null;

                // ใช้โค้ดส่วนลด
                if (!string.IsNullOrEmpty(model.PromoCode))
                {
                    var promo = await ValidatePromoCodeAsync(model.PromoCode.Trim());

                    if (promo != null && totalBase >= promo.MinimumSpend)
                    {
                        if (promo.DiscountPercent > 0)
                        {
                            discountAmount = Math.Round(totalBase * (promo.DiscountPercent / 100m), 2);
                        }
                        else if (promo.DiscountAmount > 0)
                        {
                            discountAmount = Math.Min(totalBase, promo.DiscountAmount);
                        }

                        appliedPromoCode = promo.Code;

                        // อัปเดตจำนวนการใช้งาน
                        promo.CurrentUses++;
                        _context.PromoCodes.Update(promo);
                    }
                }

                // กำหนดค่าใน Booking
                booking.PromoCode = appliedPromoCode;
                booking.DiscountAmount = discountAmount;
                booking.TotalAmount = Math.Max(0m, totalBase - discountAmount);
                booking.DepositAmount = Math.Round(booking.TotalAmount * 0.30m, 2);

                // Pre-order items
                if (model.PreOrderItems != null && model.PreOrderItems.Any())
                    booking.PreOrderItems = JsonSerializer.Serialize(model.PreOrderItems);

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // โหลดข้อมูล User สำหรับส่งกลับ
                booking.User = await _context.Users.FindAsync(userId);
                return booking;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating booking");
                throw;
            }
        }

        // เมธอดสำหรับตรวจสอบโค้ดส่วนลด (แบบสั้น)
        public async Task<PromoCode?> ValidatePromoCodeAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;

            var upperCode = code.Trim().ToUpperInvariant();

            _logger.LogInformation("Validating promo code: {Code} -> {UpperCode}", code, upperCode);

            var promo = await _context.PromoCodes
                .FirstOrDefaultAsync(p => p.Code == upperCode && p.IsActive);

            if (promo == null)
            {
                _logger.LogInformation("Promo code not found or inactive: {Code}", upperCode);
                return null;
            }

            var now = DateTime.UtcNow;
            _logger.LogInformation("Current time: {Now}, ValidFrom: {ValidFrom}, ValidTo: {ValidTo}",
                now, promo.ValidFrom, promo.ValidTo);

            if (now < promo.ValidFrom || now > promo.ValidTo)
            {
                _logger.LogInformation("Promo code expired or not yet valid: {Code}", upperCode);
                return null;
            }

            if (promo.MaxUses > 0 && promo.CurrentUses >= promo.MaxUses)
            {
                _logger.LogInformation("Promo code usage limit reached: {Code}, Uses: {CurrentUses}/{MaxUses}",
                    upperCode, promo.CurrentUses, promo.MaxUses);
                return null;
            }

            _logger.LogInformation("Promo code validation successful: {Code}", upperCode);
            return promo;
        }

        // เมธอดสำหรับตรวจสอบโค้ดส่วนลด (แบบละเอียด)
        public async Task<PromoCode?> ValidatePromoCodeAsync(
            string code,
            int branchId,
            DateTime date,
            TimeSpan startTime,
            int duration,
            int guests,
            string? zone,
            string? tableZone,
            string? tableType,
            decimal baseTotal)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;

            var promo = await _context.PromoCodes
                .FirstOrDefaultAsync(p => p.Code == code.Trim().ToUpperInvariant() && p.IsActive);

            if (promo == null) return null;

            var now = DateTime.UtcNow;
            if (now < promo.ValidFrom || now > promo.ValidTo)
                return null;

            if (promo.MaxUses > 0 && promo.CurrentUses >= promo.MaxUses)
                return null;

            if (baseTotal < promo.MinimumSpend)
                return null;

            // ตรวจสอบวันที่ใช้ได้
            if (!string.IsNullOrEmpty(promo.ApplicableDays) && promo.ApplicableDays != "[]")
            {
                var days = JsonSerializer.Deserialize<List<string>>(promo.ApplicableDays) ?? new();
                var bookingDay = date.DayOfWeek.ToString();
                if (!days.Any(d => string.Equals(d, bookingDay, StringComparison.OrdinalIgnoreCase)))
                    return null;
            }

            // ตรวจสอบโซนที่ใช้ได้
            if (!string.IsNullOrEmpty(promo.ApplicableZones) && promo.ApplicableZones != "[]"
                && !string.IsNullOrEmpty(tableZone))
            {
                var zones = JsonSerializer.Deserialize<List<string>>(promo.ApplicableZones) ?? new();
                if (!zones.Any(z => string.Equals(z, tableZone, StringComparison.OrdinalIgnoreCase)))
                    return null;
            }

            // ตรวจสอบประเภทโต๊ะที่ใช้ได้
            if (!string.IsNullOrEmpty(promo.ApplicableTableTypes) && promo.ApplicableTableTypes != "[]"
                && !string.IsNullOrEmpty(tableType))
            {
                var types = JsonSerializer.Deserialize<List<string>>(promo.ApplicableTableTypes) ?? new();
                if (!types.Any(t => string.Equals(t, tableType, StringComparison.OrdinalIgnoreCase)))
                    return null;
            }

            return promo;
        }

        public async Task<string> GenerateBookingCodeAsync()
        {
            string code;
            do
            {
                code = $"BK{DateTime.Now:yyyyMMdd}{new Random().Next(1000, 9999)}";
            }
            while (await _context.Bookings.AnyAsync(b => b.BookingCode == code));
            return code;
        }

        public async Task<bool> CancelBookingAsync(int bookingId, string userId)
        {
            var booking = await _context.Bookings
                .Include(b => b.Table)
                    .ThenInclude(t => t.Branch)
                .Include(b => b.User)
                .Include(b => b.Payment)
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.UserId == userId);

            if (booking == null) return false;

            // ✅ ใช้ DateTime.UtcNow สำหรับการเปรียบเทียบ
            var bookingDateTime = booking.BookingDate.Add(booking.StartTime);
            var hoursBeforeBooking = (bookingDateTime - DateTime.UtcNow).TotalHours;

            if (hoursBeforeBooking < 24) return false;

            booking.Status = "Cancelled";
            booking.ModifiedAt = DateTime.UtcNow;

            if (booking.Payment != null && booking.Payment.Status == "Completed")
            {
                booking.Payment.Status = "Refunded";
                booking.Payment.RefundDate = DateTime.UtcNow;
                booking.Payment.RefundAmount = booking.DepositAmount * 0.70m;
            }

            _context.Bookings.Update(booking);
            await _context.SaveChangesAsync();

            await _lineNotify.SendBookingCancellationAsync(booking);
            return true;
        }

        public async Task<bool> CheckInAsync(string bookingCode)
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.BookingCode == bookingCode && b.Status == "Confirmed");

            if (booking == null) return false;

            // ✅ ใช้ DateTime.UtcNow สำหรับการเปรียบเทียบ
            var now = DateTime.UtcNow;
            var bookingDate = DateTime.SpecifyKind(booking.BookingDate.Date, DateTimeKind.Utc);

            if (bookingDate.Date != now.Date) return false;

            var bookingDateTime = bookingDate.Add(booking.StartTime);
            if (now < bookingDateTime.AddMinutes(-30)) return false;

            booking.Status = "CheckedIn";
            booking.CheckInTime = now;

            _context.Bookings.Update(booking);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}