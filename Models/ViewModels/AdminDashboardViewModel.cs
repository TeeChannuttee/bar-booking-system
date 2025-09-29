using BarBookingSystem.Models;

namespace BarBookingSystem.Models.ViewModels
{
    public class DashboardPoint
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
    }

    public class AdminDashboardViewModel
    {
        // Today
        public int TodayBookings { get; set; }
        public int TodayCheckIns { get; set; }
        public decimal TodayRevenue { get; set; }
        public int TodayNewCustomers { get; set; }
        public int TodayPendingCheckIns { get; set; }  // ใหม่: การจองที่รอ check-in วันนี้

        // Week
        public int WeekBookings { get; set; }
        public decimal WeekRevenue { get; set; }
        public int WeekCancellations { get; set; }
        public int WeekNewCustomers { get; set; }      // ใหม่: ลูกค้าใหม่สัปดาห์นี้

        // Performance Metrics
        public double CancellationRate { get; set; }
        public decimal AverageSpend { get; set; }
        public decimal AverageRevenuePerBooking { get; set; }  // ใหม่: รายได้เฉลี่ยต่อการจอง
        public double BookingGrowthRate { get; set; }          // ใหม่: อัตราการเติบโต

        // Lists
        public List<Booking> UpcomingBookings { get; set; } = new();
        public List<Booking> RecentBookings { get; set; } = new();

        // Charts
        public Dictionary<string, int> BookingsByZone { get; set; } = new();
        public List<DashboardPoint> RevenueChart { get; set; } = new();
        public List<DashboardPoint> BookingsChart { get; set; } = new();
    }
}