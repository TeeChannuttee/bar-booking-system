namespace BarBookingSystem.Models.DTOs
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
}