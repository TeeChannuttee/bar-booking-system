using BarBookingSystem.Models;

public class PromoCodeViewModel
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Description { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal MinimumSpend { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public int MaxUses { get; set; }

    // ✅ ใช้ List เพื่อรองรับ multiple select
    public List<string> ApplicableDays { get; set; } = new();
    public List<string> ApplicableZones { get; set; } = new();
    public List<string> ApplicableTableTypes { get; set; } = new();
}