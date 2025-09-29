using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.ComponentModel.DataAnnotations;

namespace BarBookingSystem.Models
{
    public class Branch
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        [MaxLength(500)]
        public string Address { get; set; }

        [MaxLength(20)]
        public string Phone { get; set; }

        public string OpeningHours { get; set; } // JSON string {"mon":"17:00-02:00"}

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        [MaxLength(500)]
        public string ImageUrl { get; set; }

        [MaxLength(1000)]
        public string Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<Table> Tables { get; set; }
    }
}
