using BarBookingSystem.Data;
using BarBookingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace BarBookingSystem.Services
{
    public class AdminTableService : IAdminTableService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminTableService> _logger;

        public AdminTableService(ApplicationDbContext context, ILogger<AdminTableService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Table>> GetAllTablesAsync()
        {
            return await _context.Tables
                .Include(t => t.Branch)
                .OrderBy(t => t.BranchId)
                .ThenBy(t => t.TableNumber)
                .ToListAsync();
        }

        public async Task<Table> GetTableWithDetailsAsync(int id)
        {
            return await _context.Tables
                .Include(t => t.Branch)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<List<Branch>> GetActiveBranchesAsync()
        {
            return await _context.Branches
                .Where(b => b.IsActive)
                .ToListAsync();
        }

        public async Task<List<Booking>> GetRecentBookingsAsync(int tableId)
        {
            return await _context.Bookings
                .Include(b => b.User)
                .Where(b => b.TableId == tableId)
                .OrderByDescending(b => b.BookingDate)
                .Take(10)
                .ToListAsync();
        }

        public async Task<(bool Success, string Error)> CreateTableAsync(Table table)
        {
            try
            {
                // Set default values for nullable fields
                table.Notes ??= string.Empty;
                table.FloorPosition ??= string.Empty;

                // Validate branch exists
                var branch = await _context.Branches.FindAsync(table.BranchId);
                if (branch == null)
                {
                    return (false, "เลือกสาขาไม่ถูกต้อง");
                }

                table.Branch = branch;
                table.Bookings ??= new List<Booking>();

                _context.Tables.Add(table);
                await _context.SaveChangesAsync();

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating table");
                return (false, "เกิดข้อผิดพลาดในการสร้างโต๊ะ");
            }
        }

        public async Task<(bool Success, string Error)> UpdateTableAsync(int id, Table model)
        {
            try
            {
                var existingTable = await _context.Tables.FindAsync(id);
                if (existingTable == null)
                    return (false, "ไม่พบโต๊ะที่ต้องการแก้ไข");

                // Validate branch exists
                var branch = await _context.Branches.FindAsync(model.BranchId);
                if (branch == null)
                {
                    return (false, "เลือกสาขาไม่ถูกต้อง");
                }

                // Update the existing table properties
                existingTable.BranchId = model.BranchId;
                existingTable.TableNumber = model.TableNumber;
                existingTable.Zone = model.Zone;
                existingTable.TableType = model.TableType;
                existingTable.Capacity = model.Capacity;
                existingTable.MinimumSpend = model.MinimumSpend;
                existingTable.BasePrice = model.BasePrice;
                existingTable.FloorPosition = model.FloorPosition ?? string.Empty;
                existingTable.IsActive = model.IsActive;
                existingTable.Notes = model.Notes ?? string.Empty;

                _context.Update(existingTable);
                await _context.SaveChangesAsync();

                return (true, null);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await TableExistsAsync(id))
                    return (false, "ไม่พบโต๊ะที่ต้องการแก้ไข");
                else
                    throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating table {TableId}", id);
                return (false, "เกิดข้อผิดพลาดในการอัปเดตข้อมูลโต๊ะ");
            }
        }

        public async Task<(bool Success, string Error)> DeleteTableAsync(int id)
        {
            try
            {
                var table = await _context.Tables
                    .Include(t => t.Branch)
                    .Include(t => t.Bookings)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (table == null)
                    return (false, "ไม่พบโต๊ะที่ต้องการลบ");

                // Check if table has any bookings
                var hasBookings = table.Bookings.Any();
                if (hasBookings)
                {
                    return (false, "ไม่สามารถลบโต๊ะได้ เนื่องจากมีประวัติการจอง");
                }

                _context.Tables.Remove(table);
                await _context.SaveChangesAsync();

                return (true, $"ลบโต๊ะ {table.TableNumber} เรียบร้อยแล้ว");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting table {TableId}", id);
                return (false, "เกิดข้อผิดพลาดในการลบโต๊ะ");
            }
        }

        public async Task<bool> TableExistsAsync(int id)
        {
            return await _context.Tables.AnyAsync(e => e.Id == id);
        }
    }
}