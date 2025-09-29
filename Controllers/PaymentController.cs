using BarBookingSystem.Data;
using BarBookingSystem.Models;
using BarBookingSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stripe;
using Stripe.Checkout;

namespace BarBookingSystem.Controllers
{
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILineNotifyService _lineNotify;
        private readonly IEmailService _emailService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILineNotifyService lineNotify,
            IEmailService emailService,
            ILogger<PaymentController> logger)
        {
            _context = context;
            _configuration = configuration;
            _lineNotify = lineNotify;
            _emailService = emailService;
            _logger = logger;

            StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];
        }

        // GET: /Payment/ProcessPayment/5
        public async Task<IActionResult> ProcessPayment(int bookingId)
        {
            var booking = await _context.Bookings
                .Include(b => b.Table).ThenInclude(t => t.Branch)
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null) return NotFound();

            var pk = _configuration["Stripe:PublishableKey"];
            if (string.IsNullOrWhiteSpace(pk))
            {
                _logger.LogError("Stripe publishable key is missing. Check Stripe:PublishableKey");
                TempData["Error"] = "Stripe Publishable Key ไม่ถูกต้อง/หายไป";
            }
            ViewBag.StripePublishableKey = pk;

            return View(booking);
        }

        // POST: /Payment/CreatePaymentIntent
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePaymentIntent([FromForm] int bookingId)
        {
            try
            {
                _logger.LogInformation("CreatePI start: bookingId={BookingId}", bookingId);

                if (string.IsNullOrWhiteSpace(StripeConfiguration.ApiKey))
                    StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

                if (string.IsNullOrWhiteSpace(StripeConfiguration.ApiKey))
                    return Json(new { error = "Stripe Secret Key ไม่ถูกต้อง/หายไป (Stripe:SecretKey)" });

                if (bookingId <= 0)
                    return Json(new { error = "BookingId ไม่ถูกต้อง" });

                var booking = await _context.Bookings
                    .Include(b => b.User)
                    .FirstOrDefaultAsync(b => b.Id == bookingId);

                if (booking == null)
                    return Json(new { error = $"ไม่พบการจอง (BookingId={bookingId})" });

                if (!string.Equals(booking.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                    return Json(new { error = "การจองนี้ได้ชำระเงินแล้วหรือสถานะไม่พร้อมชำระ" });

                if (booking.DepositAmount <= 0)
                    return Json(new { error = "ยอดมัดจำไม่ถูกต้อง (ต้องมากกว่า 0)" });

                _logger.LogInformation("CreatePI guard ok: deposit={Deposit}, status={Status}", booking.DepositAmount, booking.Status);

                var amountInSatang = (long)Math.Round(booking.DepositAmount * 100m, MidpointRounding.AwayFromZero);

                var options = new PaymentIntentCreateOptions
                {
                    Amount = amountInSatang,
                    Currency = "thb",
                    Description = $"Deposit for booking {booking.BookingCode}",
                    Metadata = new Dictionary<string, string>
                    {
                        { "booking_id", booking.Id.ToString() },
                        { "booking_code", booking.BookingCode ?? "" },
                        { "user_email", booking.User?.Email ?? "" }
                    },
                    AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true }
                };

                var service = new PaymentIntentService();
                var paymentIntent = await service.CreateAsync(options);

                var utcNow = DateTime.UtcNow;

                var payment = await _context.Payments.FirstOrDefaultAsync(p => p.BookingId == booking.Id);
                if (payment == null)
                {
                    payment = new Payment
                    {
                        BookingId = booking.Id,
                        StripePaymentIntentId = paymentIntent.Id,
                        Amount = booking.DepositAmount,
                        Status = "Pending",
                        PaymentMethod = "Card",
                        PaymentDate = utcNow,
                        TransactionDetails = "{}",
                        Notes = "" // ✅ กัน NULL
                    };
                    _context.Payments.Add(payment);
                }
                else
                {
                    payment.StripePaymentIntentId = paymentIntent.Id;
                    payment.Amount = booking.DepositAmount;
                    payment.Status = "Pending";
                    payment.PaymentMethod = "Card";
                    payment.PaymentDate = utcNow;
                    if (string.IsNullOrWhiteSpace(payment.TransactionDetails))
                        payment.TransactionDetails = "{}"; // ✅ กัน NULL
                    _context.Payments.Update(payment);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("CreatePI done: {PaymentIntentId}", paymentIntent.Id);

                return Json(new { clientSecret = paymentIntent.ClientSecret });
            }
            catch (StripeException sex)
            {
                _logger.LogError(sex, "Stripe error creating payment intent for booking {BookingId}", bookingId);
                return Json(new { error = "Stripe error", detail = sex.Message, code = sex.StripeError?.Code, type = sex.StripeError?.Type });
            }
            catch (DbUpdateException dbex)
            {
                if (dbex.InnerException is PostgresException pex)
                {
                    _logger.LogError(dbex, "DB error creating PI for booking {BookingId}", bookingId);
                    return Json(new
                    {
                        error = "DB error",
                        sqlState = pex.SqlState,           // เช่น 23502/23505
                        constraint = pex.ConstraintName,   // ชื่อ FK/Unique ที่ชน
                        column = pex.ColumnName,           // คอลัมน์ที่พัง (ถ้ามี)
                        message = pex.MessageText,
                        detail = pex.Detail
                    });
                }

                _logger.LogError(dbex, "EF error creating PI for booking {BookingId}", bookingId);
                return Json(new { error = "EF error", detail = dbex.InnerException?.Message ?? dbex.Message });
            }
        }

        // GET: /Payment/Success
        public async Task<IActionResult> Success(int bookingId, string payment_intent)
        {
            try
            {
                var booking = await _context.Bookings
                    .Include(b => b.Table).ThenInclude(t => t.Branch)
                    .Include(b => b.User)
                    .Include(b => b.Payment)
                    .FirstOrDefaultAsync(b => b.Id == bookingId);

                if (booking == null)
                    return NotFound();

                booking.Status = "Confirmed";
                var utcNow = DateTime.UtcNow;

                if (booking.Payment == null)
                {
                    booking.Payment = new Payment
                    {
                        BookingId = booking.Id,
                        StripePaymentIntentId = payment_intent,
                        Amount = booking.DepositAmount,
                        Status = "Completed",
                        PaymentMethod = "Card",
                        PaymentDate = utcNow,
                        TransactionDetails = "{}", // ✅ กัน NULL
                        Notes = ""
                    };
                    _context.Payments.Add(booking.Payment);
                }
                else
                {
                    booking.Payment.Status = "Completed";
                    booking.Payment.StripePaymentIntentId = payment_intent;
                    booking.Payment.PaymentDate = utcNow;
                    if (string.IsNullOrWhiteSpace(booking.Payment.TransactionDetails))
                        booking.Payment.TransactionDetails = "{}"; // ✅ กัน NULL
                    _context.Payments.Update(booking.Payment);
                }

                _context.Bookings.Update(booking);
                await _context.SaveChangesAsync();

                await _lineNotify.SendBookingConfirmationAsync(booking);
                await _emailService.SendBookingConfirmationEmailAsync(booking);

                await _lineNotify.SendAdminNotificationAsync(
                    $"New Booking: {booking.BookingCode}\n" +
                    $"Table: {booking.Table.TableNumber}\n" +
                    $"Date: {booking.BookingDate:dd/MM/yyyy} {booking.StartTime}");

                TempData["Success"] = "การจองสำเร็จ! คุณจะได้รับอีเมลและ LINE แจ้งเตือนยืนยันการจอง";
                return View(booking);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in payment success for booking {BookingId}", bookingId);
                TempData["Error"] = "เกิดข้อผิดพลาดในการยืนยันการชำระเงิน";
                return RedirectToAction("ProcessPayment", new { bookingId });
            }
        }

        // POST: /Payment/StripeWebhook
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> StripeWebhook()
        {
            _logger.LogInformation("Webhook endpoint called but disabled for local testing");
            return Ok();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult TestStripeKeys()
        {
            var pk = _configuration["Stripe:PublishableKey"];
            var sk = _configuration["Stripe:SecretKey"];
            return Json(new
            {
                hasPK = !string.IsNullOrWhiteSpace(pk),
                pkPrefix = pk?.Substring(0, 7),
                hasSK = !string.IsNullOrWhiteSpace(sk),
                skPrefix = sk?.Substring(0, 7),
                env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            });
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> TestCreatePi()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(StripeConfiguration.ApiKey))
                    StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

                var service = new PaymentIntentService();
                var pi = await service.CreateAsync(new PaymentIntentCreateOptions
                {
                    Amount = 5000,           // 50.00 THB
                    Currency = "thb",
                    AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true }
                });
                return Json(new { ok = true, clientSecret = pi.ClientSecret, id = pi.Id });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message });
            }
        }
    }
}
