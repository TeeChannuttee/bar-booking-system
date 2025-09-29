using BarBookingSystem.Data;
using BarBookingSystem.Models;
using BarBookingSystem.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace BarBookingSystem.Services
{
    public class AdminPromoService : IAdminPromoService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminPromoService> _logger;

        public AdminPromoService(ApplicationDbContext context, ILogger<AdminPromoService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<PromoCode>> GetAllPromoCodesAsync()
        {
            return await _context.PromoCodes
                .OrderByDescending(p => p.ValidTo)
                .ToListAsync();
        }

        public async Task<PromoCode> GetPromoCodeAsync(int id)
        {
            return await _context.PromoCodes.FindAsync(id);
        }

        public async Task<(bool Success, string Error)> CreatePromoCodeAsync(PromoCode promoCode, PromoCodeFormData formData)
        {
            try
            {
                // ตรวจสอบ validation พื้นฐาน
                var validation = await ValidatePromoCodeAsync(promoCode, formData, isUpdate: false);
                if (!validation.IsValid)
                    return (false, validation.Error);

                // ตรวจสอบวันที่
                var dateValidation = ValidateDateRange(formData.ValidFromStr, formData.ValidToStr);
                if (!dateValidation.IsValid)
                    return (false, dateValidation.Error);

                var (validFromUtc, validToUtc) = ParseDates(formData.ValidFromStr, formData.ValidToStr, promoCode);

                // ตรวจสอบว่ามี PromoCode ซ้ำหรือไม่
                var existingCode = await _context.PromoCodes
                    .AnyAsync(p => p.Code.ToUpper() == promoCode.Code.ToUpper());
                if (existingCode)
                    return (false, $"รหัสโปรโมชัน '{promoCode.Code}' มีอยู่แล้วในระบบ");

                // Set promo code properties
                promoCode.Code = promoCode.Code?.Trim().ToUpperInvariant();
                promoCode.Description = promoCode.Description?.Trim();
                promoCode.CurrentUses = 0;
                promoCode.IsActive = true;
                promoCode.ValidFrom = validFromUtc;
                promoCode.ValidTo = validToUtc;
                promoCode.MaxUses = Math.Max(1, promoCode.MaxUses);

                // Set JSON arrays for applicable conditions
                promoCode.ApplicableDays = SerializeArray(formData.ApplicableDays);
                promoCode.ApplicableZones = SerializeArray(formData.ApplicableZones);
                promoCode.ApplicableTableTypes = SerializeArray(formData.ApplicableTableTypes);

                _context.PromoCodes.Add(promoCode);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created promo code: {PromoCode}", promoCode.Code);
                return (true, null);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error creating promo code: {PromoCode}", promoCode?.Code);

                // ตรวจสอบ inner exception เพื่อให้ error message ที่ชัดเจนขึ้น
                var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                if (innerMessage.Contains("UNIQUE") || innerMessage.Contains("duplicate"))
                    return (false, "รหัสโปรโมชันนี้มีอยู่แล้วในระบบ");
                if (innerMessage.Contains("NULL"))
                    return (false, "ข้อมูลที่จำเป็นไม่ครบถ้วน กรุณาตรวจสอบอีกครั้ง");

                return (false, "เกิดข้อผิดพลาดในฐานข้อมูล กรุณาลองใหม่อีกครั้ง");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating promo code: {PromoCode}", promoCode?.Code);
                return (false, "ไม่สามารถสร้างโปรโมชันได้: " + ex.Message);
            }
        }

        public async Task<(bool Success, string Error)> UpdatePromoCodeAsync(PromoCode promoCode, PromoCodeFormData formData)
        {
            try
            {
                var existingPromo = await _context.PromoCodes.FindAsync(promoCode.Id);
                if (existingPromo == null)
                    return (false, "ไม่พบโปรโมชันที่ต้องการแก้ไข");

                var validation = await ValidatePromoCodeAsync(promoCode, formData, isUpdate: true);
                if (!validation.IsValid)
                    return (false, validation.Error);

                var dateValidation = ValidateDateRange(formData.ValidFromStr, formData.ValidToStr);
                if (!dateValidation.IsValid)
                    return (false, dateValidation.Error);

                var (validFromUtc, validToUtc) = ParseDates(formData.ValidFromStr, formData.ValidToStr, existingPromo);

                // ตรวจสอบว่ามี PromoCode ซ้ำหรือไม่ (ยกเว้นตัวเอง)
                var duplicateCode = await _context.PromoCodes
                    .AnyAsync(p => p.Code.ToUpper() == promoCode.Code.ToUpper() && p.Id != promoCode.Id);
                if (duplicateCode)
                    return (false, $"รหัสโปรโมชัน '{promoCode.Code}' มีอยู่แล้วในระบบ");

                // Update existing promo
                existingPromo.Code = promoCode.Code?.Trim().ToUpperInvariant();
                existingPromo.Description = promoCode.Description?.Trim();
                existingPromo.DiscountPercent = promoCode.DiscountPercent;
                existingPromo.DiscountAmount = promoCode.DiscountAmount;
                existingPromo.MinimumSpend = promoCode.MinimumSpend;
                existingPromo.ValidFrom = validFromUtc;
                existingPromo.ValidTo = validToUtc;
                existingPromo.MaxUses = Math.Max(1, promoCode.MaxUses);
                existingPromo.IsActive = promoCode.IsActive;

                existingPromo.ApplicableDays = SerializeArray(formData.ApplicableDays);
                existingPromo.ApplicableZones = SerializeArray(formData.ApplicableZones);
                existingPromo.ApplicableTableTypes = SerializeArray(formData.ApplicableTableTypes);

                _context.Update(existingPromo);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated promo code: {PromoCode}", existingPromo.Code);
                return (true, null);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error updating promo code {PromoId}", promoCode.Id);

                var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                if (innerMessage.Contains("UNIQUE") || innerMessage.Contains("duplicate"))
                    return (false, "รหัสโปรโมชันนี้มีอยู่แล้วในระบบ");
                if (innerMessage.Contains("NULL"))
                    return (false, "ข้อมูลที่จำเป็นไม่ครบถ้วน กรุณาตรวจสอบอีกครั้ง");

                return (false, "เกิดข้อผิดพลาดในฐานข้อมูล กรุณาลองใหม่อีกครั้ง");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating promo code {PromoId}", promoCode.Id);
                return (false, "ไม่สามารถอัปเดตโปรโมชันได้: " + ex.Message);
            }
        }

        public async Task<(bool Success, string Error)> DeletePromoCodeAsync(int id)
        {
            try
            {
                var promoCode = await _context.PromoCodes.FindAsync(id);
                if (promoCode == null)
                    return (false, "ไม่พบโปรโมชันที่ต้องการลบ");

                // Check if promo code has been used
                if (promoCode.CurrentUses > 0)
                    return (false, "ไม่สามารถลบโปรโมชันที่มีการใช้งานแล้ว");

                _context.PromoCodes.Remove(promoCode);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted promo code: {PromoCode}", promoCode.Code);
                return (true, $"ลบโปรโมชัน {promoCode.Code} เรียบร้อยแล้ว");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting promo code {PromoId}", id);
                return (false, "เกิดข้อผิดพลาดในการลบโปรโมชัน");
            }
        }

        public async Task<(bool Success, string Message, bool IsActive)> TogglePromoStatusAsync(int id)
        {
            try
            {
                var promoCode = await _context.PromoCodes.FindAsync(id);
                if (promoCode == null)
                    return (false, "ไม่พบโปรโมชัน", false);

                promoCode.IsActive = !promoCode.IsActive;
                _context.Update(promoCode);
                await _context.SaveChangesAsync();

                var status = promoCode.IsActive ? "เปิดใช้งาน" : "ปิดใช้งาน";
                _logger.LogInformation("Toggled promo status: {PromoCode} -> {Status}", promoCode.Code, status);
                return (true, $"เปลี่ยนสถานะโปรโมชันเป็น{status}แล้ว", promoCode.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling promo status {PromoId}", id);
                return (false, "เกิดข้อผิดพลาดในการเปลี่ยนสถานะ", false);
            }
        }

        private async Task<(bool IsValid, string Error)> ValidatePromoCodeAsync(PromoCode promoCode, PromoCodeFormData formData, bool isUpdate)
        {
            // ตรวจสอบข้อมูลพื้นฐาน
            if (string.IsNullOrWhiteSpace(promoCode.Code))
                return (false, "กรุณาระบุรหัสโปรโมชัน");

            if (promoCode.Code.Length < 3 || promoCode.Code.Length > 20)
                return (false, "รหัสโปรโมชันต้องมีความยาว 3-20 ตัวอักษร");

            if (string.IsNullOrWhiteSpace(promoCode.Description))
                return (false, "กรุณาระบุคำอธิบายโปรโมชัน");

            if (promoCode.MaxUses <= 0)
                return (false, "จำนวนครั้งที่ใช้ได้สูงสุดต้องมากกว่า 0");

            // ตรวจสอบส่วนลด
            var hasPercent = promoCode.DiscountPercent > 0;
            var hasAmount = promoCode.DiscountAmount > 0;

            if (!hasPercent && !hasAmount)
                return (false, "กรุณาระบุส่วนลดเป็นเปอร์เซ็นต์หรือเป็นจำนวนเงินอย่างใดอย่างหนึ่ง");

            if (hasPercent && hasAmount)
                return (false, "ระบุส่วนลดได้เพียงแบบเดียว (เปอร์เซ็นต์ หรือ จำนวนเงิน)");

            if (promoCode.DiscountPercent < 0 || promoCode.DiscountPercent > 100)
                return (false, "เปอร์เซ็นต์ส่วนลดต้องอยู่ระหว่าง 0–100");

            if (promoCode.DiscountAmount < 0)
                return (false, "จำนวนเงินส่วนลดต้องไม่ติดลบ");

            if (promoCode.MinimumSpend < 0)
                return (false, "ยอดขั้นต่ำต้องไม่ติดลบ");

            return (true, null);
        }

        private static (bool IsValid, string Error) ValidateDateRange(string validFromStr, string validToStr)
        {
            if (string.IsNullOrWhiteSpace(validFromStr))
                return (false, "กรุณาระบุวันที่เริ่มต้น");

            if (string.IsNullOrWhiteSpace(validToStr))
                return (false, "กรุณาระบุวันที่สิ้นสุด");

            if (!DateTime.TryParseExact(validFromStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var validFrom))
                return (false, "รูปแบบวันที่เริ่มต้นไม่ถูกต้อง");

            if (!DateTime.TryParseExact(validToStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var validTo))
                return (false, "รูปแบบวันที่สิ้นสุดไม่ถูกต้อง");

            if (validTo.Date < validFrom.Date)
                return (false, "วันที่สิ้นสุดต้องไม่ก่อนวันที่เริ่ม");

            return (true, null);
        }

        private static (DateTime validFromUtc, DateTime validToUtc) ParseDates(string validFromStr, string validToStr, PromoCode fallbackPromo)
        {
            DateTime vfLocal, vtLocal;

            // Parse ValidFrom
            if (!string.IsNullOrWhiteSpace(validFromStr) &&
                DateTime.TryParseExact(validFromStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out vfLocal))
            {
                // ใช้วันที่ที่ parse ได้
            }
            else
            {
                vfLocal = fallbackPromo?.ValidFrom != default(DateTime) ? fallbackPromo.ValidFrom : DateTime.UtcNow.Date;
            }

            // Parse ValidTo
            if (!string.IsNullOrWhiteSpace(validToStr) &&
                DateTime.TryParseExact(validToStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out vtLocal))
            {
                // ใช้วันที่ที่ parse ได้
            }
            else
            {
                vtLocal = fallbackPromo?.ValidTo != default(DateTime) ? fallbackPromo.ValidTo : vfLocal.AddDays(30);
            }

            // Convert to UTC covering full days
            var vfUtc = DateTime.SpecifyKind(vfLocal.Date, DateTimeKind.Utc);
            var vtUtc = DateTime.SpecifyKind(vtLocal.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

            return (vfUtc, vtUtc);
        }

        private static string SerializeArray(string[] array)
        {
            return (array != null && array.Length > 0)
                ? System.Text.Json.JsonSerializer.Serialize(array)
                : "[]";
        }
    }
}