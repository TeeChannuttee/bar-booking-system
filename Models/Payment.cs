using System.ComponentModel.DataAnnotations;

namespace BarBookingSystem.Models
{
    public class Payment
    {
        public int Id { get; set; }

        public int BookingId { get; set; }

        [MaxLength(100)]
        public string StripePaymentIntentId { get; set; }

        [Required]
        [MaxLength(50)]
        public string PaymentMethod { get; set; } // Card, Cash, Transfer

        [Range(0, 1000000)]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } // Pending, Completed, Failed, Refunded

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        public DateTime? RefundDate { get; set; }

        [Range(0, 1000000)]
        public decimal RefundAmount { get; set; }

        public string TransactionDetails { get; set; } // JSON

        [MaxLength(500)]
        public string Notes { get; set; }

        // Navigation properties
        public virtual Booking Booking { get; set; }
    }
}
