using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BarBookingSystem.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        public DateTime? DateOfBirth { get; set; }

        [MaxLength(20)]
        public string? MemberTier { get; set; } = "Bronze";

        public int LoyaltyPoints { get; set; } = 0;

        [MaxLength(500)]
        public string? Address { get; set; }

        public string? LineUserId { get; set; }

        public bool NotificationEnabled { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastLoginAt { get; set; }

        // ⭐ Properties สำหรับสถิติ (ไม่เก็บในฐานข้อมูล)
        [NotMapped]
        public int TotalBookings { get; set; }

        [NotMapped]
        public int CompletedBookings { get; set; }

        [NotMapped]
        public int CancelledBookings { get; set; }

        // Navigation
        public virtual ICollection<Booking>? Bookings { get; set; }
    }
}