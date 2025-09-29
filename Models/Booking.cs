using System.ComponentModel.DataAnnotations;

namespace BarBookingSystem.Models
{
    public class Booking
    {
        public int Id { get; set; }

        [Required, MaxLength(20)]
        public string BookingCode { get; set; } = default!; // BK20241225001

        public string UserId { get; set; } = default!;

        public int TableId { get; set; }

        // ----- บังคับให้เป็น UTC ด้วย backing field -----
        private DateTime _bookingDate;
        [Required]
        public DateTime BookingDate
        {
            get => _bookingDate;
            set => _bookingDate = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        [Range(1, 50)]
        public int NumberOfGuests { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Confirmed, CheckedIn, Completed, Cancelled, NoShow

        [Range(0, 1_000_000)]
        public decimal TotalAmount { get; set; }

        [Range(0, 1_000_000)]
        public decimal DepositAmount { get; set; }

        [MaxLength(1000)]
        public string? SpecialRequests { get; set; }

        [MaxLength(50)]
        public string? PromoCode { get; set; }

        [Range(0, 100_000)]
        public decimal DiscountAmount { get; set; }

        // ใช้ Utc ตอนสร้าง
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ---- nullable DateTime ทั้งหมดบังคับ Kind=Utc ----
        private DateTime? _modifiedAt;
        public DateTime? ModifiedAt
        {
            get => _modifiedAt;
            set => _modifiedAt = value.HasValue
                ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
                : (DateTime?)null;
        }

        private DateTime? _checkInTime;
        public DateTime? CheckInTime
        {
            get => _checkInTime;
            set => _checkInTime = value.HasValue
                ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
                : (DateTime?)null;
        }

        private DateTime? _checkOutTime;
        public DateTime? CheckOutTime
        {
            get => _checkOutTime;
            set => _checkOutTime = value.HasValue
                ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
                : (DateTime?)null;
        }
        // -----------------------------------------------

        [MaxLength(100)]
        public string? QRCodeData { get; set; } // For check-in

        public string? PreOrderItems { get; set; } // JSON

        public bool ReminderSent { get; set; } = false;

        public string? GoogleCalendarEventId { get; set; }

        // Navigation
        public virtual ApplicationUser User { get; set; } = default!;
        public virtual Table Table { get; set; } = default!;
        public virtual Payment? Payment { get; set; }
    }
}
