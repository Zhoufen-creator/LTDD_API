namespace tran_nguyen_vi_khang_api.Services.Interfaces;

public interface IEmailSenderService
{
    Task<bool> SendOtpEmailAsync(string recipientEmail, string otp);
}

