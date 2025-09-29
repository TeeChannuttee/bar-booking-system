using System.ComponentModel.DataAnnotations;

namespace BarBookingSystem.Models
{
    public class Table
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string TableNumber { get; set; }

        public int BranchId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Zone { get; set; } // Indoor, Outdoor, Smoking, VIP, Private

        [Required]
        [MaxLength(50)]
        public string TableType { get; set; } // Standard, VIP, Private

        [Range(1, 50)]
        public int Capacity { get; set; }

        [Range(0, 100000)]
        public decimal MinimumSpend { get; set; }

        [Range(0, 10000)]
        public decimal BasePrice { get; set; }

        [MaxLength(50)]
        public string FloorPosition { get; set; } // X,Y coordinates for floor plan

        public bool IsActive { get; set; } = true;

        [MaxLength(500)]
        public string Notes { get; set; }

        // Navigation properties
        public virtual Branch Branch { get; set; }
        public virtual ICollection<Booking> Bookings { get; set; }
    }
}
