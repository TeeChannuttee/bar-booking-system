using BarBookingSystem.Data;
using BarBookingSystem.Models;              // Branch, Table, ApplicationUser
using BarBookingSystem.Models.ViewModels;  // CreateBookingViewModel
using BarBookingSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Text.Json;

namespace BarBookingSystem.Controllers
{
    [Authorize]
    public class BookingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IBookingService _bookingService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILineNotifyService _lineNotify;
        private readonly IEmailService _emailService;
        private readonly ILogger<BookingController> _logger;

        public BookingController(
            ApplicationDbContext context,
            IBookingService bookingService,
            UserManager<ApplicationUser> userManager,
            ILineNotifyService lineNotify,
            IEmailService emailService,
            ILogger<BookingController> logger)
        {
            _context = context;
            _bookingService = bookingService;
            _userManager = userManager;
            _lineNotify = lineNotify;
            _emailService = emailService;
            _logger = logger;
        }

        // GET: /Booking/Create
        public async Task<IActionResult> Create()
        {
            var model = new CreateBookingViewModel
            {
                AvailableBranches = await _context.Branches.Where(b => b.IsActive).ToListAsync(),
                BookingDate = DateTime.Today.AddDays(1)
            };

            return View(model);
        }

        // POST: /Booking/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateBookingViewModel model)
        {
            // helper: จะตอบ JSON ถ้าเป็น AJAX
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            // รวม error จาก ModelState ให้เห็นง่าย ๆ
            IEnumerable<string> ModelErrors() =>
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);

            try
            {
                // 0) validate model เบื้องต้น
                if (!ModelState.IsValid)
                {
                    if (isAjax)
                        return Json(new { success = false, message = "MODEL_INVALID", errors = ModelErrors() });

                    model.AvailableBranches = await _context.Branches.Where(b => b.IsActive).ToListAsync();
                    return View(model);
                }

                // 1) ดึง user
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    if (isAjax) return Json(new { success = false, message = "USER_NOT_FOUND" });
                    ModelState.AddModelError("", "ไม่พบผู้ใช้งาน");
                    model.AvailableBranches = await _context.Branches.Where(b => b.IsActive).ToListAsync();
                    return View(model);
                }

                // 2) parse เวลา
                if (!TimeSpan.TryParse(model.StartTime, out var startTs))
                {
                    if (isAjax) return Json(new { success = false, message = "BAD_START_TIME" });
                    ModelState.AddModelError(nameof(model.StartTime), "เวลาเริ่มไม่ถูกต้อง");
                    model.AvailableBranches = await _context.Branches.Where(b => b.IsActive).ToListAsync();
                    return View(model);
                }
                var endTs = startTs.Add(TimeSpan.FromHours(model.Duration));

                // 3) ตรวจโต๊ะที่เลือก
                if (model.TableId == null)
                {
                    if (isAjax) return Json(new { success = false, message = "TABLE_NOT_SELECTED" });
                    ModelState.AddModelError("", "กรุณาเลือกโต๊ะ");
                    model.AvailableBranches = await _context.Branches.Where(b => b.IsActive).ToListAsync();
                    return View(model);
                }

                var table = await _context.Tables
                    .Include(t => t.Branch)
                    .FirstOrDefaultAsync(t => t.Id == model.TableId.Value);

                if (table == null)
                {
                    if (isAjax) return Json(new { success = false, message = "TABLE_NOT_FOUND" });
                    ModelState.AddModelError("", "ไม่พบข้อมูลโต๊ะ");
                    model.AvailableBranches = await _context.Branches.Where(b => b.IsActive).ToListAsync();
                    return View(model);
                }

                // 4) ตรวจโต๊ะว่าง (กัน submit ข้าม client)
                var available = await _bookingService.GetAvailableTablesAsync(
                    model.BranchId!.Value, model.BookingDate.Date, startTs, model.Duration, model.NumberOfGuests, model.Zone);

                if (!available.Any(t => t.Id == model.TableId))
                {
                    if (isAjax) return Json(new { success = false, message = "TABLE_NOT_AVAILABLE" });
                    ModelState.AddModelError("", "โต๊ะที่เลือกไม่ว่างในช่วงเวลานี้");
                    model.AvailableBranches = await _context.Branches.Where(b => b.IsActive).ToListAsync();
                    return View(model);
                }

                // 5) คำนวณราคา
                decimal totalBase = table.MinimumSpend + table.BasePrice;
                decimal discount = 0m;
                string? appliedCode = null;

                if (!string.IsNullOrWhiteSpace(model.PromoCode))
                {
                    // ตัดความยาวกันชนกับความยาวคอลัมน์ใน DB (ถ้า DB กำหนดสั้น เช่น nvarchar(32))
                    var code = model.PromoCode.Trim().ToUpperInvariant();
                    if (code.Length > 32) code = code.Substring(0, 32);

                    var promo = await _bookingService.ValidatePromoCodeAsync(code);
                    if (promo != null && totalBase >= promo.MinimumSpend)
                    {
                        if (promo.DiscountPercent > 0)
                            discount = Math.Min(totalBase, Math.Round(totalBase * (promo.DiscountPercent / 100m), 2));
                        else if (promo.DiscountAmount > 0)
                            discount = Math.Min(totalBase, promo.DiscountAmount);

                        appliedCode = promo.Code;

                        // ❗️บันทึกการใช้ไว้ตอนนี้ (ถ้าคุณอยากย้ายไปตอนชำระเงินเสร็จ ให้ย้ายบรรทัดนี้กับ Update ด้านล่าง)
                        promo.CurrentUses += 1;
                        _context.PromoCodes.Update(promo);
                    }
                }

                // 6) สร้าง booking entity
                var booking = new Booking
                {
                    BookingCode = await _bookingService.GenerateBookingCodeAsync(),
                    UserId = user.Id,                       // ❗️สำคัญ: ต้องไม่ null และ FK ต้องชี้ไป Users ได้จริง
                    TableId = model.TableId.Value,          // ❗️ต้องแม็พกับ Table.Id ได้
                    BookingDate = model.BookingDate.Date,       // เก็บเป็น date (00:00)
                    StartTime = startTs,
                    EndTime = endTs,
                    NumberOfGuests = model.NumberOfGuests,
                    Status = "Pending",
                    SpecialRequests = string.IsNullOrWhiteSpace(model.SpecialRequests)
                                      ? null
                                      : (model.SpecialRequests.Length > 1000
                                            ? model.SpecialRequests.Substring(0, 1000)  // กันตัดเกินความยาวคอลัมน์
                                            : model.SpecialRequests),
                    PromoCode = appliedCode,                  // null ถ้าไม่ได้ใช้หรือไม่ผ่าน
                    DiscountAmount = discount,
                    TotalAmount = totalBase,                    // เก็บยอด “ก่อนหัก” หรือ “หลังหัก” ก็ได้ แต่ให้สอดคล้องในระบบ
                    DepositAmount = Math.Round((totalBase - discount) * 0.30m, 2),
                    CreatedAt = DateTime.UtcNow,
                    QRCodeData = Guid.NewGuid().ToString()
                };

                if (model.PreOrderItems != null && model.PreOrderItems.Any())
                    booking.PreOrderItems = JsonSerializer.Serialize(model.PreOrderItems);

                _context.Bookings.Add(booking);

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException dbex)
                {
                    // ดึง inner / entries ชัด ๆ
                    var inner = dbex.InnerException?.Message ?? "no inner";
                    var entries = _context.ChangeTracker.Entries()
                        .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
                        .Select(e => new
                        {
                            e.Entity.GetType().Name,
                            e.State,
                            CurrentValues = e.CurrentValues.Properties.ToDictionary(
                                p => p.Name, p => e.CurrentValues[p.Name])
                        });

                    _logger.LogError(dbex, "DB UPDATE ERROR while saving booking. Inner={Inner}. Entries={@Entries}", inner, entries);

                    if (isAjax)
                        return Json(new
                        {
                            success = false,
                            message = "DB_UPDATE_ERROR",
                            inner,
                            entries
                        });

                    ModelState.AddModelError("", $"บันทึกไม่สำเร็จ: {inner}");
                    model.AvailableBranches = await _context.Branches.Where(b => b.IsActive).ToListAsync();
                    return View(model);
                }

                // สำเร็จ
                if (isAjax)
                {
                    return Json(new
                    {
                        success = true,
                        bookingId = booking.Id,
                        redirectUrl = Url.Action("ProcessPayment", "Payment", new { bookingId = booking.Id })
                    });
                }

                return RedirectToAction("ProcessPayment", "Payment", new { bookingId = booking.Id });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? "no inner";
                _logger.LogError(ex, "Create booking failed. Inner={Inner}", inner);

                if (isAjax)
                {
                    return Json(new
                    {
                        success = false,
                        message = "EXCEPTION",
                        error = ex.Message,
                        inner,
                        stack = ex.StackTrace
                    });
                }

                ModelState.AddModelError("", $"เกิดข้อผิดพลาด: {ex.Message} - {inner}");
                model.AvailableBranches = await _context.Branches.Where(b => b.IsActive).ToListAsync();
                return View(model);
            }
        }

        // GET: /Booking/MyBookings
        public async Task<IActionResult> MyBookings()
        {
            var user = await _userManager.GetUserAsync(User);

            var bookings = await _context.Bookings
                .Include(b => b.Table)
                    .ThenInclude(t => t.Branch)
                .Include(b => b.Payment)
                .Where(b => b.UserId == user.Id)
                .OrderByDescending(b => b.BookingDate)
                .ThenByDescending(b => b.StartTime)
                .ToListAsync();

            return View(bookings);
        }

        // GET: /Booking/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            var booking = await _context.Bookings
                .Include(b => b.Table)
                    .ThenInclude(t => t.Branch)
                .Include(b => b.Payment)
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == user.Id);

            if (booking == null)
                return NotFound();

            var viewModel = new BookingDetailsViewModel
            {
                Booking = booking,
                CanCancel = booking.Status == "Confirmed" &&
                           (booking.BookingDate.Add(booking.StartTime) - DateTime.Now).TotalHours >= 24,
                CanModify = booking.Status == "Confirmed" &&
                           (booking.BookingDate.Add(booking.StartTime) - DateTime.Now).TotalHours >= 48,
                RefundAmount = booking.DepositAmount * 0.70m
            };

            // Generate QR Code
            using (var qrGenerator = new QRCodeGenerator())
            {
                var qrCodeData = qrGenerator.CreateQrCode(booking.BookingCode, QRCodeGenerator.ECCLevel.Q);

                // ใช้ byte renderer (ไม่ต้องใช้คลาส QRCode และไม่ต้องอาศัย System.Drawing)
                var pngQr = new PngByteQRCode(qrCodeData);
                var pngBytes = pngQr.GetGraphic(10);  // 10 = pixel per module

                viewModel.QRCodeBase64 = Convert.ToBase64String(pngBytes);
            }


            return View(viewModel);
        }

        // POST: /Booking/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            var success = await _bookingService.CancelBookingAsync(id, user.Id);

            if (success)
            {
                TempData["Success"] = "การจองถูกยกเลิกเรียบร้อยแล้ว คุณจะได้รับเงินคืน 70% ภายใน 7-14 วันทำการ";
            }
            else
            {
                TempData["Error"] = "ไม่สามารถยกเลิกการจองได้ เนื่องจากเหลือเวลาน้อยกว่า 24 ชั่วโมง";
            }

            return RedirectToAction(nameof(MyBookings));
        }

        // API: Get Available Tables
        // API: Get Available Tables
        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> GetAvailableTables(
     int branchId, string date, string time, int duration, int guests, string? zone = null)
        {
            try
            {
                _logger.LogInformation(
                    "GetAvailableTables INPUT: branchId={BranchId}, date={Date}, time={Time}, duration={Duration}, guests={Guests}, zone={Zone}",
                    branchId, date, time, duration, guests, zone);

                if (!DateTime.TryParse(date, out var bookingDateLocal))
                    return Ok(Array.Empty<object>());
                if (!TimeSpan.TryParse(time, out var startTime))
                    return Ok(Array.Empty<object>());

                var bookingDateUtc = DateTime.SpecifyKind(bookingDateLocal.Date, DateTimeKind.Utc);

                // ✅ service จะกรอง zone ให้เองแล้ว
                var tables = await _bookingService.GetAvailableTablesAsync(
                    branchId, bookingDateUtc, startTime, duration, guests, zone);

                _logger.LogInformation("Service returned {Count} tables", tables.Count);

                // ✅ ถ้าไม่เจอโต๊ะเลย
                if (tables.Count == 0)
                {
                    // ❌ ถ้าเลือก zone แล้วไม่เจอ → ห้าม fallback
                    if (!string.IsNullOrWhiteSpace(zone))
                    {
                        _logger.LogInformation("No tables found for branch {BranchId} in zone {Zone}", branchId, zone);
                        return Ok(Array.Empty<object>());
                    }

                    // ✅ fallback เฉพาะกรณีไม่ได้เลือก zone
                    var probe = await _context.Tables
                        .Include(t => t.Branch)
                        .Where(t => t.IsActive
                                    && t.BranchId == branchId
                                    && t.Capacity >= guests)
                        .OrderBy(t => t.TableNumber)
                        .Take(10)
                        .ToListAsync();

                    if (probe.Count > 0)
                    {
                        var probeResult = probe.Select(t => new
                        {
                            id = t.Id,
                            tableNumber = t.TableNumber,
                            zone = t.Zone,
                            tableType = t.TableType,
                            capacity = t.Capacity,
                            minimumSpend = t.MinimumSpend,
                            basePrice = t.BasePrice,
                            total = t.MinimumSpend + t.BasePrice,
                            debug = "FALLBACK"
                        }).ToList();

                        return Ok(probeResult);
                    }
                }

                // ✅ return โต๊ะที่หาได้
                var result = tables.Select(t => new
                {
                    id = t.Id,
                    tableNumber = t.TableNumber,
                    zone = t.Zone,
                    tableType = t.TableType,
                    capacity = t.Capacity,
                    minimumSpend = t.MinimumSpend,
                    basePrice = t.BasePrice,
                    total = t.MinimumSpend + t.BasePrice
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "GetAvailableTables error. branchId={BranchId}, date={Date}, time={Time}",
                    branchId, date, time);
                return Ok(Array.Empty<object>());
            }
        }


        [HttpGet]
        [Produces("application/json")]

        public async Task<IActionResult> DebugAvailableTables(
    int branchId, string date, string time, int duration, int guests, string? zone,
    [FromServices] IBookingService svc)
        {
            // parse อ่อนโยน
            if (!DateTime.TryParse(date, out var bookingDate))
                return Ok(new { error = "bad date" });
            bookingDate = DateTime.SpecifyKind(bookingDate, DateTimeKind.Utc);

            if (!TimeSpan.TryParse(time, out var startTime))
                return Ok(new { error = "bad time" });
            var endTime = startTime.Add(TimeSpan.FromHours(duration));

            // ดึงโต๊ะทั้งหมดใน branch
            var allInBranch = await _context.Tables
                .Include(t => t.Branch)
                .Where(t => t.BranchId == branchId)
                .OrderBy(t => t.Zone).ThenBy(t => t.TableNumber)
                .ToListAsync();

            // booking ที่ซ้อนช่วงเวลา
            var bookedIds = await _context.Bookings
                .Where(b => b.Table.BranchId == branchId
                            && b.BookingDate.Date == bookingDate.Date
                            && b.Status != "Cancelled"
                            && !(b.EndTime <= startTime || b.StartTime >= endTime))
                .Select(b => b.TableId)
                .Distinct()
                .ToListAsync();

            var report = allInBranch.Select(t => new
            {
                t.Id,
                t.TableNumber,
                t.Zone,
                t.TableType,
                t.BranchId,
                t.IsActive,
                t.Capacity,
                capacityOk = t.Capacity >= guests,
                zoneOk = string.IsNullOrWhiteSpace(zone) ||
                         string.Equals(t.Zone, zone, StringComparison.OrdinalIgnoreCase),
                booked = bookedIds.Contains(t.Id),
                available =
                    t.IsActive &&
                    t.Capacity >= guests &&
                    (string.IsNullOrWhiteSpace(zone) || string.Equals(t.Zone, zone, StringComparison.OrdinalIgnoreCase)) &&
                    !bookedIds.Contains(t.Id)
            }).ToList();

            return Ok(new
            {
                input = new
                {
                    branchId,
                    date = bookingDate.ToString("yyyy-MM-dd"),
                    time = startTime.ToString(@"hh\:mm"),
                    duration,
                    guests,
                    zone
                },
                totals = new
                {
                    allInBranch = allInBranch.Count,
                    bookedCount = bookedIds.Count,
                    availableCount = report.Count(x => x.available)
                },
                tables = report
            });
        }
        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> ValidatePromoCode(string code, decimal baseTotal)
        {
            try
            {
                _logger.LogInformation("Validating promo code: {Code}, baseTotal: {BaseTotal}", code, baseTotal);

                if (string.IsNullOrWhiteSpace(code))
                    return Json(new { valid = false, message = "EMPTY_CODE" });

                // ✅ ใช้ BookingService แทนการเช็คโดยตรง
                var promo = await _bookingService.ValidatePromoCodeAsync(code.Trim());

                _logger.LogInformation("Promo found: {PromoFound}", promo != null);

                if (promo == null)
                {
                    _logger.LogInformation("Promo code not found or expired: {Code}", code);
                    return Json(new { valid = false, message = "NOT_FOUND" });
                }

                _logger.LogInformation("Promo details - Code: {Code}, MinSpend: {MinSpend}, DiscountPercent: {DiscountPercent}, DiscountAmount: {DiscountAmount}",
                    promo.Code, promo.MinimumSpend, promo.DiscountPercent, promo.DiscountAmount);

                // ✅ เช็คยอดขั้นต่ำ
                if (baseTotal < promo.MinimumSpend)
                {
                    _logger.LogInformation("Base total {BaseTotal} less than minimum spend {MinSpend}", baseTotal, promo.MinimumSpend);
                    return Json(new { valid = false, message = "MINIMUM_SPEND_NOT_MET", minimum = promo.MinimumSpend });
                }

                var type = promo.DiscountPercent > 0 ? "percent" : "amount";
                var value = promo.DiscountPercent > 0 ? promo.DiscountPercent : promo.DiscountAmount;

                _logger.LogInformation("Promo code valid - Type: {Type}, Value: {Value}", type, value);

                return Json(new
                {
                    valid = true,
                    description = promo.Description,
                    type,
                    value
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ValidatePromoCode error for code={Code}", code);
                return Json(new { valid = false, message = "ERROR" });
            }
        }
    }
}