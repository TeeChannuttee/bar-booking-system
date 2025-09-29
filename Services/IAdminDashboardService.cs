using BarBookingSystem.Models.ViewModels;

namespace BarBookingSystem.Services
{
    public interface IAdminDashboardService
    {
        Task<AdminDashboardViewModel> GetDashboardDataAsync();
    }
}