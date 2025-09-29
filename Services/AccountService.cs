using BarBookingSystem.Data;
using BarBookingSystem.Models;
using BarBookingSystem.Models.ViewModels;
using BarBookingSystem.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BarBookingSystem.Services
{
    public class AccountService : IAccountService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountService> _logger;

        public AccountService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext context,
            ILogger<AccountService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _logger = logger;
        }

        public async Task<(bool Success, string[] Errors)> RegisterUserAsync(RegisterViewModel model)
        {
            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                PhoneNumber = model.PhoneNumber,
                DateOfBirth = model.DateOfBirth.ToUtc(),
                LineUserId = model.LineUserId ?? "",
                CreatedAt = DateTime.UtcNow,
                MemberTier = "Bronze",
                LoyaltyPoints = 0,
                NotificationEnabled = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Customer");
                await _signInManager.SignInAsync(user, isPersistent: false);
                _logger.LogInformation($"New user registered: {user.Email}");
                return (true, Array.Empty<string>());
            }

            return (false, result.Errors.Select(e => e.Description).ToArray());
        }

        public async Task<(bool Success, string Error)> LoginUserAsync(LoginViewModel model)
        {
            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, false);

            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                user.LastLoginAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
                _logger.LogInformation($"User logged in: {model.Email}");
                return (true, null);
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning($"User account locked out: {model.Email}");
                return (false, "Lockout");
            }

            return (false, "อีเมลหรือรหัสผ่านไม่ถูกต้อง");
        }

        public async Task<ApplicationUser> GetUserWithStatsAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                var bookings = await _context.Bookings.Where(b => b.UserId == userId).ToListAsync();
                user.SetBookingStats(bookings);
            }
            return user;
        }

        public async Task<(bool Success, string Error)> UpdateProfileAsync(ApplicationUser user, ApplicationUser model)
        {
            try
            {
                user.UpdateFromModel(model);
                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    _logger.LogInformation($"Profile updated for user: {user.Email}");
                    return (true, null);
                }

                return (false, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating profile for user: {user?.Email}");
                return (false, "เกิดข้อผิดพลาดในการอัปเดตโปรไฟล์");
            }
        }

        public async Task<(bool Success, string Error)> ChangePasswordAsync(ApplicationUser user, string currentPassword, string newPassword, string confirmPassword)
        {
            var validation = ValidatePasswordChange(currentPassword, newPassword, confirmPassword);
            if (!validation.IsValid) return (false, validation.Error);

            try
            {
                var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
                if (result.Succeeded)
                {
                    _logger.LogInformation($"Password changed for user: {user.Email}");
                    await _signInManager.RefreshSignInAsync(user);
                    return (true, null);
                }

                return (false, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password");
                return (false, "เกิดข้อผิดพลาดในการเปลี่ยนรหัสผ่าน");
            }
        }

        public (string nextTier, int bookingsNeeded, int progressPercent) CalculateNextTierInfo(string currentTier, int completedBookings)
        {
            return currentTier switch
            {
                "Bronze" => GetTierProgress("Silver", 5, completedBookings),
                "Silver" => GetTierProgress("Gold", 15, completedBookings),
                "Gold" => GetTierProgress("Platinum", 30, completedBookings),
                _ => ("Platinum", 0, 100)
            };
        }

        private (string tier, int needed, int progress) GetTierProgress(string targetTier, int requiredBookings, int currentBookings)
        {
            var needed = Math.Max(0, requiredBookings - currentBookings);
            var progress = Math.Min(100, (int)((double)currentBookings / requiredBookings * 100));
            return (targetTier, needed, progress);
        }

        private (bool IsValid, string Error) ValidatePasswordChange(string current, string newPassword, string confirm)
        {
            if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(newPassword))
                return (false, "กรุณากรอกข้อมูลให้ครบถ้วน");

            if (newPassword != confirm)
                return (false, "รหัสผ่านใหม่และรหัสผ่านยืนยันไม่ตรงกัน");

            if (newPassword.Length < 6)
                return (false, "รหัสผ่านใหม่ต้องมีความยาวอย่างน้อย 6 ตัวอักษร");

            return (true, null);
        }
    }
}