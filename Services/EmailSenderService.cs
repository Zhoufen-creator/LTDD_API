using System.Net;
using System.Net.Mail;
using tran_nguyen_vi_khang_api.Services.Interfaces;

namespace tran_nguyen_vi_khang_api.Services;

public class EmailSenderService : IEmailSenderService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailSenderService> _logger;
    private readonly IWebHostEnvironment? _environment;

    public EmailSenderService(
        IConfiguration configuration,
        ILogger<EmailSenderService> logger,
        IWebHostEnvironment? environment = null)
    {
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
    }

    public async Task<bool> SendOtpEmailAsync(string recipientEmail, string otp)
    {
        var smtpHost = _configuration["Smtp:Host"] ?? _configuration["SMTP_HOST"];
        var smtpPortStr = _configuration["Smtp:Port"] ?? _configuration["SMTP_PORT"];
        var smtpUser = _configuration["Smtp:Username"] ?? _configuration["SMTP_USERNAME"];
        var smtpPass = _configuration["Smtp:Password"] ?? _configuration["SMTP_PASSWORD"];
        var fromEmail = _configuration["Smtp:From"] ?? _configuration["SMTP_FROM"] ?? smtpUser ?? "noreply@parentalcontrol.local";

        var maskedEmail = MaskEmail(recipientEmail);

        if (string.IsNullOrWhiteSpace(smtpHost) || !int.TryParse(smtpPortStr, out var smtpPort) || string.IsNullOrWhiteSpace(smtpUser))
        {
            _logger.LogWarning("Cannot send OTP email to {MaskedEmail}: SMTP configuration (Host, Port, Username) is missing.", maskedEmail);

            // In Development: log the plain OTP so that manual testing remains possible
            // without configuring a real SMTP server. The fallback OTP "123456" in AuthService
            // also handles automated tests, but logging the actual OTP allows realistic flow testing.
            if (_environment?.IsDevelopment() == true)
            {
                _logger.LogWarning("[DEV ONLY] OTP for {MaskedEmail} (not sent via email): {Otp}", maskedEmail, otp);
                return true;
            }

            return false;
        }

        try
        {
            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(smtpUser, smtpPass)
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, "Parental Control"),
                Subject = "Mã xác nhận đăng ký tài khoản Parental Control",
                Body = $@"Xin chào,

Mã OTP xác thực đăng ký tài khoản của bạn là: {otp}

Mã này có hiệu lực trong vòng 5 phút. Vui lòng không chia sẻ mã này với bất kỳ ai để bảo vệ an toàn cho tài khoản.

Trân trọng,
Đội ngũ Parental Control",
                IsBodyHtml = false
            };

            mailMessage.To.Add(recipientEmail);
            await client.SendMailAsync(mailMessage);

            _logger.LogInformation("OTP email successfully dispatched via SMTP to {MaskedEmail}.", maskedEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send OTP email via SMTP to {MaskedEmail}.", maskedEmail);
            return false;
        }
    }

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) return "***";
        var parts = email.Split('@');
        var name = parts[0];
        var domain = parts[1];
        var maskedName = name.Length <= 2 ? name[0] + "*" : name[0] + new string('*', name.Length - 2) + name[^1];
        return $"{maskedName}@{domain}";
    }
}

