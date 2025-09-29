using BarBookingSystem.Data;
using BarBookingSystem.Models;
using BarBookingSystem.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BarBookingSystem.Services
{
    public interface IAccountService
    {
        Task<(bool Success, string[] Errors)> RegisterUserAsync(RegisterViewModel model);
        Task<(bool Success, string Error)> LoginUserAsync(LoginViewModel model);
        Task<ApplicationUser> GetUserWithStatsAsync(string userId);
        Task<(bool Success, string Error)> UpdateProfileAsync(ApplicationUser user, ApplicationUser model);
        Task<(bool Success, string Error)> ChangePasswordAsync(ApplicationUser user, string currentPassword, string newPassword, string confirmPassword);
        (string nextTier, int bookingsNeeded, int progressPercent) CalculateNextTierInfo(string currentTier, int completedBookings);
    }
}