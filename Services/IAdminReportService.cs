using BarBookingSystem.Models.ViewModels;

namespace BarBookingSystem.Services
{
    public interface IAdminReportService
    {
        Task<ReportsViewModel> GenerateReportsAsync(DateTime startDate, DateTime endDate);
        Task<byte[]> ExportBookingsToCsvAsync(DateTime? startDate, DateTime? endDate);
    }
}