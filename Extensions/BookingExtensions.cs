using BarBookingSystem.Models;

namespace BarBookingSystem.Extensions
{
    public static class BookingExtensions
    {
        public static void UpdateFromModel(this Booking booking, Booking model)
        {
            booking.TableId = model.TableId;
            booking.BookingDate = model.BookingDate.Kind == DateTimeKind.Utc
                ? model.BookingDate
                : DateTime.SpecifyKind(model.BookingDate.Date, DateTimeKind.Utc);
            booking.StartTime = model.StartTime;
            booking.EndTime = model.EndTime;
            booking.NumberOfGuests = model.NumberOfGuests;
            booking.Status = model.Status;
            booking.SpecialRequests = string.IsNullOrWhiteSpace(model.SpecialRequests) ? null : model.SpecialRequests;
            booking.ModifiedAt = DateTime.UtcNow;
        }
    }
}