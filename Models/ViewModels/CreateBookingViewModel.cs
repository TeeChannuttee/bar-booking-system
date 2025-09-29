using System.ComponentModel.DataAnnotations;
using BarBookingSystem.Models;

namespace BarBookingSystem.Models.ViewModels
{
    public class CreateBookingViewModel
    {
        public int? BranchId { get; set; }
        public List<Branch>? AvailableBranches { get; set; }

        public int? TableId { get; set; }
        public List<Table>? AvailableTables { get; set; }

        [Required, DataType(DataType.Date)]
        public DateTime BookingDate { get; set; } = DateTime.Today.AddDays(1);

        [Required]
        public string StartTime { get; set; } = "19:00";

        [Required, Range(1, 5)]
        public int Duration { get; set; } = 2;

        [Required, Range(1, 20)]
        public int NumberOfGuests { get; set; } = 2;

        public string? Zone { get; set; }
        public string? TableType { get; set; }

        [MaxLength(1000)]
        public string? SpecialRequests { get; set; }

        [MaxLength(50)]
        public string? PromoCode { get; set; }

        public List<PreOrderItem>? PreOrderItems { get; set; }

        public decimal EstimatedTotal { get; set; }
        public decimal DepositRequired { get; set; }
        public decimal DiscountAmount { get; set; }
    }

    public class PreOrderItem
    {
        public string ItemName { get; set; } = "";
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal Total => Price * Quantity;
    }
}
