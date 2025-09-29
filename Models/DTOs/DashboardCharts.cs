namespace BarBookingSystem.Models.DTOs
{
    public class DashboardCharts
    {
        public List<DashboardPoint> RevenueChart { get; set; } = new();
        public Dictionary<string, int> BookingsByZone { get; set; } = new();
    }

    public class DashboardPoint
    {
        public string Label { get; set; }
        public decimal Value { get; set; }
    }
}