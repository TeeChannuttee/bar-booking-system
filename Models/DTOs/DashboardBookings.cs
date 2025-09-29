using BarBookingSystem.Models;

namespace BarBookingSystem.Models.DTOs
{
    public class DashboardBookings
    {
        public List<Booking> Upcoming { get; set; } = new();
        public List<Booking> Recent { get; set; } = new();
    }
}