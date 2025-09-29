using System;
using System.Collections.Generic;

namespace BarBookingSystem.Models.ViewModels
{
    public class ReportsViewModel
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public decimal TotalRevenue { get; set; }
        public int TotalBookings { get; set; }
        public double CancellationRate { get; set; }
        public decimal AvgPerBooking => TotalBookings > 0 ? TotalRevenue / TotalBookings : 0;

        public List<string> ChartLabels { get; set; } = new();
        public List<decimal> ChartData { get; set; } = new();

        public List<string> ZoneLabels { get; set; } = new();
        public List<int> ZoneData { get; set; } = new();

        public List<TopTableDto> TopTables { get; set; } = new();
        public List<TopCustomerDto> TopCustomers { get; set; } = new();
    }

    public class TopTableDto
    {
        public int Rank { get; set; }
        public string TableNumber { get; set; }
        public string BranchName { get; set; }
        public int BookingCount { get; set; }
    }

    public class TopCustomerDto
    {
        public int Rank { get; set; }
        public string Name { get; set; }
        public int BookingCount { get; set; }
        public decimal TotalSpent { get; set; }
    }
}
