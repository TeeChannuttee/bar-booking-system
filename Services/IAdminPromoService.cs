using BarBookingSystem.Models;

namespace BarBookingSystem.Services
{
    public interface IAdminPromoService
    {
        Task<List<PromoCode>> GetAllPromoCodesAsync();
        Task<PromoCode> GetPromoCodeAsync(int id);
        Task<(bool Success, string Error)> CreatePromoCodeAsync(PromoCode promoCode, PromoCodeFormData formData);
        Task<(bool Success, string Error)> UpdatePromoCodeAsync(PromoCode promoCode, PromoCodeFormData formData);
        Task<(bool Success, string Error)> DeletePromoCodeAsync(int id);
        Task<(bool Success, string Message, bool IsActive)> TogglePromoStatusAsync(int id);
    }

    public class PromoCodeFormData
    {
        public string ValidFromStr { get; set; }
        public string ValidToStr { get; set; }
        public string[] ApplicableDays { get; set; }
        public string[] ApplicableZones { get; set; }
        public string[] ApplicableTableTypes { get; set; }
    }
}