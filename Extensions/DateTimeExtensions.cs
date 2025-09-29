using BarBookingSystem.Models;

namespace BarBookingSystem.Extensions
{
    public static class DateTimeExtensions
    {
        public static DateTime GetDayStart(this DateTime dateTime)
        {
            return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, 0, 0, 0, DateTimeKind.Utc);
        }

        public static DateTime GetWeekStart(this DateTime dateTime)
        {
            return dateTime.GetDayStart().AddDays(-(int)dateTime.DayOfWeek);
        }

        public static DateTime GetMonthStart(this DateTime dateTime)
        {
            return new DateTime(dateTime.Year, dateTime.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        }
    }
}