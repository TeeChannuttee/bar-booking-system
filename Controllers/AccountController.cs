using BarBookingSystem.Data;
using BarBookingSystem.Models;
using BarBookingSystem.Models.ViewModels;
using BarBookingSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace BarBookingSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAccountService _accountService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            IAccountService accountService,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<AccountController> logger)
        {
            _accountService = accountService;
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        public IActionResult Register() => View();

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            model.LineUserId ??= "";
            ModelState.Remove("LineUserId");

            if (!ModelState.IsValid) return View(model);

            var (success, errors) = await _accountService.RegisterUserAsync(model);
            if (success)
            {
                TempData["Success"] = "สมัครสมาชิกเรียบร้อยแล้ว";
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in errors)
                ModelState.AddModelError(string.Empty, error);

            return View(model);
        }

        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            model.ReturnUrl = model.ReturnUrl ?? returnUrl ?? "";
            ModelState.Remove("ReturnUrl");
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid) return View(model);

            var (success, error) = await _accountService.LoginUserAsync(model);

            if (success)
            {
                TempData["Success"] = "เข้าสู่ระบบเรียบร้อยแล้ว";
                return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                    ? Redirect(returnUrl)
                    : RedirectToAction("Index", "Home");
            }

            if (error == "Lockout") return View("Lockout");

            ModelState.AddModelError(string.Empty, error);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out");
            TempData["Success"] = "ออกจากระบบเรียบร้อยแล้ว";
            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _accountService.GetUserWithStatsAsync(_userManager.GetUserId(User));
            if (user == null) return NotFound();

            var (nextTier, bookingsNeeded, progressPercent) = _accountService.CalculateNextTierInfo(user.MemberTier, user.CompletedBookings);

            ViewBag.NextTier = nextTier;
            ViewBag.BookingsToNextTier = bookingsNeeded;
            ViewBag.ProgressToNextTier = progressPercent;

            // ✅ เคลียร์ ViewBag stats เพื่อป้องกันการแสดงซ้ำ
            ViewBag.TotalBookings = user.TotalBookings;
            ViewBag.CompletedBookings = user.CompletedBookings;
            ViewBag.CancelledBookings = user.CancelledBookings;

            return View(user);
        }

        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(ApplicationUser model)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    TempData["Error"] = "ไม่พบข้อมูลผู้ใช้";
                    return RedirectToAction(nameof(Profile));
                }

                var (success, error) = await _accountService.UpdateProfileAsync(user, model);

                // ✅ ใช้ TempData เฉพาะครั้งเดียว พร้อม key ที่ไม่ซ้ำ
                if (success)
                {
                    TempData["ProfileUpdateSuccess"] = "อัปเดตโปรไฟล์เรียบร้อยแล้ว";
                    _logger.LogInformation("Profile updated successfully for user {UserId}", user.Id);
                }
                else
                {
                    TempData["ProfileUpdateError"] = $"เกิดข้อผิดพลาด: {error}";
                    _logger.LogWarning("Profile update failed for user {UserId}: {Error}", user.Id, error);
                }

                // ✅ ต้อง redirect เพื่อใช้ PRG pattern และป้องกัน resubmission
                return RedirectToAction(nameof(Profile));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during profile update");
                TempData["ProfileUpdateError"] = "เกิดข้อผิดพลาดไม่คาดคิด กรุณาลองใหม่อีกครั้ง";
                return RedirectToAction(nameof(Profile));
            }
        }

        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    TempData["PasswordChangeError"] = "ไม่พบข้อมูลผู้ใช้";
                    return RedirectToAction(nameof(Profile));
                }

                var (success, error) = await _accountService.ChangePasswordAsync(user, currentPassword, newPassword, confirmPassword);

                // ✅ ใช้ TempData key ที่แตกต่างจาก profile update
                if (success)
                {
                    TempData["PasswordChangeSuccess"] = "เปลี่ยนรหัสผ่านเรียบร้อยแล้ว";
                    _logger.LogInformation("Password changed successfully for user {UserId}", user.Id);
                }
                else
                {
                    TempData["PasswordChangeError"] = $"ไม่สามารถเปลี่ยนรหัสผ่านได้: {error}";
                    _logger.LogWarning("Password change failed for user {UserId}: {Error}", user.Id, error);
                }

                return RedirectToAction(nameof(Profile));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during password change");
                TempData["PasswordChangeError"] = "เกิดข้อผิดพลาดไม่คาดคิด กรุณาลองใหม่อีกครั้ง";
                return RedirectToAction(nameof(Profile));
            }
        }

        public IActionResult AccessDenied() => View();

        [HttpGet, Authorize]
        public async Task<IActionResult> GetProfileData()
        {
            var user = await _accountService.GetUserWithStatsAsync(_userManager.GetUserId(User));
            if (user == null) return NotFound();

            return Json(new
            {
                user.FullName,
                user.Email,
                user.PhoneNumber,
                user.MemberTier,
                user.LoyaltyPoints,
                user.TotalBookings,
                user.CompletedBookings,
                user.CancelledBookings
            });
        }
    }
}