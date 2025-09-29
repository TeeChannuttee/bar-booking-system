using BarBookingSystem.Data;
using BarBookingSystem.Models;
using BarBookingSystem.Models.ViewModels;
using BarBookingSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BarBookingSystem.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class AdminController : Controller
    {
        private readonly IAdminDashboardService _dashboardService;
        private readonly IAdminBookingService _bookingService;
        private readonly IAdminTableService _tableService;
        private readonly IAdminPromoService _promoService;
        private readonly IAdminReportService _reportService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IAdminDashboardService dashboardService,
            IAdminBookingService bookingService,
            IAdminTableService tableService,
            IAdminPromoService promoService,
            IAdminReportService reportService,
            ApplicationDbContext context,
            ILogger<AdminController> logger)
        {
            _dashboardService = dashboardService;
            _bookingService = bookingService;
            _tableService = tableService;
            _promoService = promoService;
            _reportService = reportService;
            _context = context;
            _logger = logger;
        }

        // GET: /Admin/Dashboard
        public async Task<IActionResult> Dashboard() => View(await _dashboardService.GetDashboardDataAsync());

        // GET: /Admin/Bookings
        public async Task<IActionResult> Bookings(string status = null, DateTime? date = null, string search = null)
        {
            var bookings = await _bookingService.GetBookingsAsync(status, date, search);
            ViewBag.Status = status;
            ViewBag.Date = date;
            ViewBag.Search = search;
            return View(bookings);
        }

        // GET: /Admin/CheckIn
        public async Task<IActionResult> CheckIn()
        {
            var recentCheckIns = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Table).ThenInclude(t => t.Branch)
                .Where(b => b.Status == "CheckedIn" && b.CheckInTime != null)
                .OrderByDescending(b => b.CheckInTime)
                .Take(10)
                .ToListAsync();

            ViewBag.RecentCheckIns = recentCheckIns;
            return View();
        }

        // POST: /Admin/CheckIn
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckIn(string bookingCode)
        {
            var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                         Request.Headers["Content-Type"].ToString().Contains("application/json");

            var result = await _bookingService.ProcessCheckInAsync(bookingCode);

            if (isAjax)
                return Json(new { success = result.Success, message = result.Message });

            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(CheckIn));
        }

        // GET: /Admin/Tables
        public async Task<IActionResult> Tables() => View(await _tableService.GetAllTablesAsync());

        // GET: /Admin/CreateTable
        public async Task<IActionResult> CreateTable()
        {
            ViewBag.Branches = await _tableService.GetActiveBranchesAsync();
            return View();
        }

        // POST: /Admin/CreateTable
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTable(Table table)
        {
            // Remove navigation properties from ModelState
            ModelState.Remove(nameof(Table.Branch));
            ModelState.Remove(nameof(Table.Bookings));

            if (!ModelState.IsValid)
            {
                ViewBag.Branches = await _tableService.GetActiveBranchesAsync();
                return View(table);
            }

            var result = await _tableService.CreateTableAsync(table);
            if (result.Success)
            {
                TempData["Success"] = "เพิ่มโต๊ะเรียบร้อยแล้ว";
                return RedirectToAction(nameof(Tables));
            }

            TempData["Error"] = result.Error;
            ViewBag.Branches = await _tableService.GetActiveBranchesAsync();
            return View(table);
        }

        // GET: /Admin/EditTable/5
        public async Task<IActionResult> EditTable(int id)
        {
            var table = await _tableService.GetTableWithDetailsAsync(id);
            if (table == null) return NotFound();

            ViewBag.Branches = await _tableService.GetActiveBranchesAsync();
            ViewBag.RecentBookings = await _tableService.GetRecentBookingsAsync(id);
            return View(table);
        }

        // POST: /Admin/EditTable/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTable(int id, Table model)
        {
            if (id != model.Id) return NotFound();

            // Remove navigation properties from ModelState
            ModelState.Remove(nameof(Table.Branch));
            ModelState.Remove(nameof(Table.Bookings));

            if (!ModelState.IsValid)
            {
                ViewBag.Branches = await _tableService.GetActiveBranchesAsync();
                ViewBag.RecentBookings = await _tableService.GetRecentBookingsAsync(id);
                return View(model);
            }

            var result = await _tableService.UpdateTableAsync(id, model);
            TempData[result.Success ? "Success" : "Error"] = result.Success
            ? "สร้างโปรโมชันเรียบร้อยแล้ว"  // ← ปัญหาตรงนี้!
            : result.Error;

            return result.Success ? RedirectToAction(nameof(Tables)) : View(model);
        }

        // GET: /Admin/DeleteTable/5
        public async Task<IActionResult> DeleteTable(int id)
        {
            var result = await _tableService.DeleteTableAsync(id);
            TempData[result.Success ? "Success" : "Error"] = result.Success
                ? result.Error  // Success message is in Error field
                : result.Error;
            return RedirectToAction(nameof(Tables));
        }

        // GET: /Admin/EditBooking/5
        public async Task<IActionResult> EditBooking(int id)
        {
            var booking = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Table).ThenInclude(t => t.Branch)
                .Include(b => b.Payment)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null) return NotFound();

            if (booking.User == null || booking.Table == null)
            {
                TempData["Error"] = "ไม่พบข้อมูลการจองที่สมบูรณ์";
                return RedirectToAction(nameof(Bookings));
            }

            ViewBag.Tables = await _context.Tables
                .Where(t => t.BranchId == booking.Table.BranchId && t.IsActive)
                .OrderBy(t => t.TableNumber)
                .ToListAsync();

            booking.SpecialRequests ??= "";
            booking.PromoCode ??= "";

            return View(booking);
        }

        // POST: /Admin/EditBooking/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBooking(int id, Booking model)
        {
            if (id != model.Id) return NotFound();

            // Remove navigation properties from ModelState
            ModelState.Remove(nameof(Booking.User));
            ModelState.Remove(nameof(Booking.Table));
            ModelState.Remove(nameof(Booking.Payment));
            ModelState.Remove(nameof(Booking.PreOrderItems));
            ModelState.Remove(nameof(Booking.QRCodeData));

            if (!ModelState.IsValid)
            {
                var existingBooking = await _context.Bookings
                    .Include(b => b.User)
                    .Include(b => b.Table).ThenInclude(t => t.Branch)
                    .Include(b => b.Payment)
                    .FirstOrDefaultAsync(b => b.Id == id);

                if (existingBooking?.Table != null)
                {
                    ViewBag.Tables = await _context.Tables
                        .Where(t => t.BranchId == existingBooking.Table.BranchId && t.IsActive)
                        .OrderBy(t => t.TableNumber)
                        .ToListAsync();
                }

                return View(existingBooking ?? model);
            }

            var result = await _bookingService.UpdateBookingAsync(id, model);
            TempData[result.Success ? "Success" : "Error"] = result.Success
                ? "อัปเดตการจองเรียบร้อยแล้ว"
                : result.Error;

            return result.Success ? RedirectToAction(nameof(Bookings)) : RedirectToAction(nameof(EditBooking), new { id });
        }

        // GET: /Admin/CancelBooking/5
        public async Task<IActionResult> CancelBooking(int id)
        {
            var result = await _bookingService.CancelBookingAsync(id);
            TempData[result ? "Success" : "Error"] = result
                ? "ยกเลิกการจองเรียบร้อยแล้ว"
                : "ไม่สามารถยกเลิกการจองนี้ได้";
            return RedirectToAction(nameof(Bookings));
        }

        // GET: /Admin/PromoCodes
        public async Task<IActionResult> PromoCodes() => View(await _promoService.GetAllPromoCodesAsync());

        // POST: /Admin/CreatePromoCode
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePromoCode(
            [FromForm] PromoCode promoCode,
            [FromForm(Name = "ValidFrom")] string validFromStr,
            [FromForm(Name = "ValidTo")] string validToStr,
            [FromForm(Name = "ApplicableDays")] string[] applicableDays,
            [FromForm(Name = "ApplicableZones")] string[] applicableZones,
            [FromForm(Name = "ApplicableTableTypes")] string[] applicableTypes)
        {
            var formData = new PromoCodeFormData
            {
                ValidFromStr = validFromStr,
                ValidToStr = validToStr,
                ApplicableDays = applicableDays,
                ApplicableZones = applicableZones,
                ApplicableTableTypes = applicableTypes
            };

            var result = await _promoService.CreatePromoCodeAsync(promoCode, formData);
            TempData[result.Success ? "Success" : "Error"] = result.Success
                ? "สร้างโปรโมชันเรียบร้อยแล้ว"
                : result.Error;
            return RedirectToAction(nameof(PromoCodes));
        }

        // GET: /Admin/EditPromoCode/5
        public async Task<IActionResult> EditPromoCode(int id)
        {
            var promoCode = await _promoService.GetPromoCodeAsync(id);
            if (promoCode == null) return NotFound();

            return Json(new
            {
                success = true,
                data = new
                {
                    Id = promoCode.Id,
                    Code = promoCode.Code,
                    Description = promoCode.Description,
                    DiscountPercent = promoCode.DiscountPercent,
                    DiscountAmount = promoCode.DiscountAmount,
                    MinimumSpend = promoCode.MinimumSpend,
                    ValidFrom = promoCode.ValidFrom.ToString("yyyy-MM-dd"),
                    ValidTo = promoCode.ValidTo.ToString("yyyy-MM-dd"),
                    MaxUses = promoCode.MaxUses,
                    CurrentUses = promoCode.CurrentUses,
                    IsActive = promoCode.IsActive,
                    ApplicableDays = promoCode.ApplicableDays,
                    ApplicableZones = promoCode.ApplicableZones,
                    ApplicableTableTypes = promoCode.ApplicableTableTypes
                }
            });
        }

        // POST: /Admin/UpdatePromoCode
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePromoCode(
            [FromForm] PromoCode promoCode,
            [FromForm(Name = "ValidFrom")] string validFromStr,
            [FromForm(Name = "ValidTo")] string validToStr,
            [FromForm(Name = "ApplicableDays")] string[] applicableDays,
            [FromForm(Name = "ApplicableZones")] string[] applicableZones,
            [FromForm(Name = "ApplicableTableTypes")] string[] applicableTypes)
        {
            var formData = new PromoCodeFormData
            {
                ValidFromStr = validFromStr,
                ValidToStr = validToStr,
                ApplicableDays = applicableDays,
                ApplicableZones = applicableZones,
                ApplicableTableTypes = applicableTypes
            };

            var result = await _promoService.UpdatePromoCodeAsync(promoCode, formData);
            TempData[result.Success ? "Success" : "Error"] = result.Success
                ? "อัปเดตโปรโมชันเรียบร้อยแล้ว"
                : result.Error;
            return RedirectToAction(nameof(PromoCodes));
        }

        // POST: /Admin/DeletePromoCode/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePromoCode(int id)
        {
            var result = await _promoService.DeletePromoCodeAsync(id);
            return Json(new
            {
                success = result.Success,
                message = result.Success ? result.Error : result.Error // Success message is in Error field
            });
        }

        // POST: /Admin/TogglePromoStatus/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePromoStatus(int id)
        {
            var result = await _promoService.TogglePromoStatusAsync(id);
            return Json(new
            {
                success = result.Success,
                message = result.Message,
                isActive = result.IsActive
            });
        }

        // GET: /Admin/Reports
        public async Task<IActionResult> Reports(DateTime? startDate, DateTime? endDate)
        {
            var endUtc = endDate?.Date ?? DateTime.Today;
            var startUtc = startDate?.Date ?? endUtc.AddDays(-30);

            var model = await _reportService.GenerateReportsAsync(startUtc, endUtc);
            ViewBag.StartDate = startUtc;
            ViewBag.EndDate = endUtc;
            return View(model);
        }

        // GET: /Admin/ExportBookings
        public async Task<IActionResult> ExportBookings(DateTime? startDate, DateTime? endDate)
        {
            var csv = await _reportService.ExportBookingsToCsvAsync(startDate, endDate);
            return File(csv, "text/csv", $"bookings_{DateTime.Now:yyyyMMdd}.csv");
        }
    }
}