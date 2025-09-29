using BarBookingSystem.Models;

namespace BarBookingSystem.Extensions
{
    public static class ApplicationUserExtensions
    {
        public static DateTime? ToUtc(this DateTime? dateTime)
        {
            return dateTime.HasValue ? DateTime.SpecifyKind(dateTime.Value, DateTimeKind.Utc) : null;
        }

        public static void UpdateFromModel(this ApplicationUser user, ApplicationUser model)
        {
            user.FullName = model.FullName?.Trim();
            user.PhoneNumber = model.PhoneNumber?.Trim();
            user.Address = model.Address?.Trim();
            user.LineUserId = model.LineUserId?.Trim();
            user.NotificationEnabled = model.NotificationEnabled;

            if (model.DateOfBirth.HasValue)
            {
                user.DateOfBirth = DateTime.SpecifyKind(model.DateOfBirth.Value.Date, DateTimeKind.Utc);
            }
        }

        public static void SetBookingStats(this ApplicationUser user, List<Booking> bookings)
        {
            user.TotalBookings = bookings.Count;
            user.CompletedBookings = bookings.Count(b => b.Status == "CheckedIn" || b.Status == "Completed");
            user.CancelledBookings = bookings.Count(b => b.Status == "Cancelled");
        }
    }
}