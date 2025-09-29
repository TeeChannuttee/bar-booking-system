using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BarBookingSystem.Models;
using BarBookingSystem.Services;

public class LineNotifyService : ILineNotifyService
{
    private readonly HttpClient _http;
    private readonly ILogger<LineNotifyService> _logger;

    // Messaging API config
    private readonly string? _channelAccessToken;        
    private readonly bool _enabled;                      
    private readonly string? _defaultRecipientId;        
    private readonly string[] _adminRecipientIds;        

    public LineNotifyService(IConfiguration config, ILogger<LineNotifyService> logger)
    {
        _http = new HttpClient();
        _logger = logger;

        
        _channelAccessToken = config["Line:ChannelAccessToken"];
        _enabled = bool.TryParse(config["Line:Enabled"], out var en) && en;
        _defaultRecipientId = config["Line:DefaultRecipientId"]; 

        var adminCsv = config["Line:AdminRecipientIds"] ?? "";
        _adminRecipientIds = adminCsv
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (_enabled && string.IsNullOrWhiteSpace(_channelAccessToken))
        {
            _logger.LogWarning("LINE Messaging: Enabled แต่ไม่มี ChannelAccessToken");
        }
    }

    // ================== Core push ==================
    private async Task PushTextAsync(string to, string message)
    {
        if (!_enabled)
        {
            _logger.LogInformation("LINE Messaging: disabled (skip send)");
            return;
        }
        if (string.IsNullOrWhiteSpace(_channelAccessToken))
        {
            _logger.LogWarning("LINE Messaging: missing ChannelAccessToken");
            return;
        }
        if (string.IsNullOrWhiteSpace(to))
        {
            _logger.LogWarning("LINE Messaging: missing recipient id");
            return;
        }

        var payload = new
        {
            to,
            messages = new[]
            {
                new { type = "text", text = message }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _channelAccessToken);

        var resp = await _http.PostAsync("https://api.line.me/v2/bot/message/push", content);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            _logger.LogError("LINE Messaging push failed: {Status} {Body}", resp.StatusCode, body);
        }
        else
        {
            _logger.LogInformation("LINE Messaging push OK to {To}", to);
        }
    }

    private Task PushDefaultAsync(string message)
        => PushTextAsync(_defaultRecipientId ?? string.Empty, message);

    // ================== ILineNotifyService impl ==================
    public Task SendMessageAsync(string message) => PushDefaultAsync(message);

    public Task SendAdminNotificationAsync(string message)
    {
        // ส่งหาหลายแอดมิน (ถ้ากำหนด), ถ้าไม่มีก็ส่งไป default
        if (_adminRecipientIds.Length == 0)
        {
            return PushDefaultAsync($"🔔 [Admin Alert]\n\n{message}\n\nTime: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        }

        var text = $"🔔 [Admin Alert]\n\n{message}\n\nTime: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
        return Task.WhenAll(_adminRecipientIds.Select(id => PushTextAsync(id, text)));
    }

    public Task SendBookingConfirmationAsync(Booking booking)
    {
        var text = $"""
🎉 ยืนยันการจองโต๊ะ

📍 สาขา: {booking.Table?.Branch?.Name ?? "-"}
📅 วันที่: {booking.BookingDate:dd/MM/yyyy}
⏰ เวลา: {booking.StartTime:hh\:mm} - {booking.EndTime:hh\:mm}
🪑 โต๊ะ: {booking.Table?.TableNumber ?? "-"} ({booking.Table?.Zone ?? "-"})
👥 จำนวน: {booking.NumberOfGuests} ท่าน
💰 ยอดมัดจำ: {booking.DepositAmount:N0} บาท

รหัสการจอง: {booking.BookingCode}
กรุณาแสดง QR Code เมื่อมาถึงร้าน
""";

        // ถ้ามี LineUserId ในโปรไฟล์ลูกค้าให้ส่งตรงไปยังผู้ใช้คนนั้น ไม่งั้นส่งไป default/group
        var to = booking.User?.LineUserId ?? _defaultRecipientId ?? string.Empty;
        return PushTextAsync(to, text);
    }

    public Task SendBookingReminderAsync(Booking booking)
    {
        var hoursUntil = (booking.BookingDate.Add(booking.StartTime) - DateTime.Now).TotalHours;
        var text = $"""
⏰ แจ้งเตือนการจอง (เหลืออีก {hoursUntil:F0} ชม.)

📍 สาขา: {booking.Table?.Branch?.Name ?? "-"}
📅 วันที่: {booking.BookingDate:dd/MM/yyyy}
⏰ เวลา: {booking.StartTime:hh\:mm}
🪑 โต๊ะ: {booking.Table?.TableNumber ?? "-"}
👥 จำนวน: {booking.NumberOfGuests} ท่าน

รหัสการจอง: {booking.BookingCode}
หากมาไม่ได้ กรุณายกเลิกล่วงหน้า ≥ 24 ชม. (คืนเงินมัดจำ 70%)
""";

        var to = booking.User?.LineUserId ?? _defaultRecipientId ?? string.Empty;
        return PushTextAsync(to, text);
    }

    public Task SendBookingCancellationAsync(Booking booking)
    {
        var refund = (booking.DepositAmount * 0.7m).ToString("N0");
        var text = $"""
❌ ยกเลิกการจอง

รหัสการจอง: {booking.BookingCode}
📅 วันที่: {booking.BookingDate:dd/MM/yyyy}
⏰ เวลา: {booking.StartTime:hh\:mm}

💰 เงินคืน: {refund} บาท (ภายใน 7–14 วันทำการ)
หวังว่าจะได้บริการคุณในโอกาสหน้า 🙏
""";

        var to = booking.User?.LineUserId ?? _defaultRecipientId ?? string.Empty;
        return PushTextAsync(to, text);
    }
}
