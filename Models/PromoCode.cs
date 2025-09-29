using System.ComponentModel.DataAnnotations;

namespace BarBookingSystem.Models
{
    public class PromoCode
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Code { get; set; }

        [Required]
        [MaxLength(200)]
        public string Description { get; set; }

        [Range(0, 100)]
        public decimal DiscountPercent { get; set; }

        [Range(0, 10000)]
        public decimal DiscountAmount { get; set; }

        [Range(0, 100000)]
        public decimal MinimumSpend { get; set; }

        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }

        [Range(0, 10000)]
        public int MaxUses { get; set; }
        public int CurrentUses { get; set; } = 0;

        // ✅ เอา [Required] ออก และให้ default เป็น [] 
        public string ApplicableZones { get; set; } = "[]";
        public string ApplicableTableTypes { get; set; } = "[]";
        public string ApplicableDays { get; set; } = "[]";

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
