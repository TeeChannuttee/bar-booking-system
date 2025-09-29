using BarBookingSystem.Data;
using BarBookingSystem.Models;
using BarBookingSystem.Models.ViewModels;
using BarBookingSystem.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BarBookingSystem.Services
{
    public class DashboardStats
    {
        public int TodayBookings { get; set; }
        public int TodayCheckIns { get; set; }
        public int TodayPendingCheckIns { get; set; }
        public int TodayNewCustomers { get; set; }
        public int WeekBookings { get; set; }
        public int WeekCancellations { get; set; }
        public int WeekNewCustomers { get; set; }
        public decimal TodayRevenue { get; set; }
        public decimal WeekRevenue { get; set; }
    }

    public class DashboardCharts
    {
        public List<DashboardPoint> RevenueChart { get; set; }
        public Dictionary<string, int> BookingsByZone { get; set; }
    }

    public class DashboardBookings
    {
        public List<Booking> Upcoming { get; set; }
        public List<Booking> Recent { get; set; }
    }
}
