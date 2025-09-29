using BarBookingSystem.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;


namespace BarBookingSystem.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                using var client = new SmtpClient();

                await client.ConnectAsync(
                    _configuration["Email:Host"],
                    int.Parse(_configuration["Email:Port"]),
                    SecureSocketOptions.StartTls);

                await client.AuthenticateAsync(
                    _configuration["Email:Username"],
                    _configuration["Email:Password"]);

                var message = new MimeMessage();
                message.From.Add(MailboxAddress.Parse(_configuration["Email:From"]));
                message.To.Add(MailboxAddress.Parse(to));
                message.Subject = subject;

                var builder = new BodyBuilder { HtmlBody = body };
                message.Body = builder.ToMessageBody();

                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation($"Email sent to {to}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email to {to}");
                throw;
            }
        }

        public async Task SendBookingConfirmationEmailAsync(Booking booking)
        {
            var subject = $"ยืนยันการจองโต๊ะ - {booking.BookingCode}";

            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, sans-serif; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); 
                   color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f8f9fa; padding: 30px; }}
        .details {{ background: white; padding: 20px; border-radius: 10px; margin: 20px 0; }}
        .detail-row {{ display: flex; justify-content: space-between; padding: 10px 0; 
                       border-bottom: 1px solid #e9ecef; }}
        .detail-row:last-child {{ border-bottom: none; }}
        .footer {{ text-align: center; padding: 20px; color: #6c757d; }}
        .btn {{ display: inline-block; padding: 12px 30px; background: #764ba2; 
                color: white; text-decoration: none; border-radius: 25px; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🍹 ยืนยันการจองโต๊ะ</h1>
            <p>ขอบคุณที่เลือกใช้บริการ Bar Booking</p>
        </div>
        
        <div class='content'>
            <p>สวัสดีคุณ {booking.User.FullName},</p>
            <p>การจองของคุณได้รับการยืนยันเรียบร้อยแล้ว</p>
            
            <div class='details'>
                <h3>📋 รายละเอียดการจอง</h3>
                <div class='detail-row'>
                    <span>รหัสการจอง:</span>
                    <strong>{booking.BookingCode}</strong>
                </div>
                <div class='detail-row'>
                    <span>สาขา:</span>
                    <strong>{booking.Table.Branch.Name}</strong>
                </div>
                <div class='detail-row'>
                    <span>วันที่:</span>
                    <strong>{booking.BookingDate:dddd dd MMMM yyyy}</strong>
                </div>
                <div class='detail-row'>
                    <span>เวลา:</span>
                    <strong>{booking.StartTime:hh\:mm} - {booking.EndTime:hh\:mm}</strong>
                </div>
                <div class='detail-row'>
                    <span>โต๊ะ:</span>
                    <strong>{booking.Table.TableNumber} ({booking.Table.Zone})</strong>
                </div>
                <div class='detail-row'>
                    <span>จำนวนท่าน:</span>
                    <strong>{booking.NumberOfGuests}</strong>
                </div>
                <div class='detail-row'>
                    <span>ยอดมัดจำ:</span>
                    <strong>{booking.DepositAmount:N0} บาท</strong>
                </div>
            </div>
            
            <center>
                <a href='{_configuration["AppSettings:BaseUrl"]}/Booking/Details/{booking.Id}' class='btn'>
                    ดูรายละเอียดการจอง
                </a>
            </center>
            
            <div class='details' style='background: #fff3cd; border-left: 4px solid #ffc107;'>
                <h4>⚠️ หมายเหตุสำคัญ</h4>
                <ul>
                    <li>กรุณามาถึงร้านตรงเวลา หากมาสาย 30 นาที การจองจะถูกยกเลิกอัตโนมัติ</li>
                    <li>หากต้องการยกเลิก กรุณาแจ้งล่วงหน้าอย่างน้อย 24 ชั่วโมง</li>
                    <li>แสดง QR Code หรือรหัสการจองเมื่อมาถึงร้าน</li>
                </ul>
            </div>
        </div>
        
        <div class='footer'>
            <p>📍 {booking.Table.Branch.Address}</p>
            <p>📞 {booking.Table.Branch.Phone}</p>
            <p style='margin-top: 20px; font-size: 12px;'>
                © 2024 Bar Booking System. All rights reserved.
            </p>
        </div>
    </div>
</body>
</html>";

            await SendEmailAsync(booking.User.Email, subject, body);
        }

        public async Task SendPasswordResetEmailAsync(string email, string resetLink)
        {
            var subject = "รีเซ็ตรหัสผ่าน - Bar Booking";
            var body = $@"
                <h2>รีเซ็ตรหัสผ่าน</h2>
                <p>คลิกลิงก์ด้านล่างเพื่อรีเซ็ตรหัสผ่านของคุณ:</p>
                <p><a href='{resetLink}'>รีเซ็ตรหัสผ่าน</a></p>
                <p>ลิงก์นี้จะหมดอายุใน 24 ชั่วโมง</p>
            ";

            await SendEmailAsync(email, subject, body);
        }
    }
}
