using System.ComponentModel.DataAnnotations;

namespace BarBookingSystem.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = "";

        [Required]
        [Phone]
        public string PhoneNumber { get; set; } = "";

        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "รหัสผ่านไม่ตรงกัน")]
        public string ConfirmPassword { get; set; } = "";

        // ✅ เพิ่ม default value และทำให้เป็น nullable
        public string? LineUserId { get; set; } = "";

        public bool AgreeTerms { get; set; }
    }
}