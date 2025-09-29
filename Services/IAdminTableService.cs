using BarBookingSystem.Models;

namespace BarBookingSystem.Services
{
    public interface IAdminTableService
    {
        Task<List<Table>> GetAllTablesAsync();
        Task<Table> GetTableWithDetailsAsync(int id);
        Task<List<Branch>> GetActiveBranchesAsync();
        Task<List<Booking>> GetRecentBookingsAsync(int tableId);
        Task<(bool Success, string Error)> CreateTableAsync(Table table);
        Task<(bool Success, string Error)> UpdateTableAsync(int id, Table model);
        Task<(bool Success, string Error)> DeleteTableAsync(int id);
        Task<bool> TableExistsAsync(int id);
    }
}