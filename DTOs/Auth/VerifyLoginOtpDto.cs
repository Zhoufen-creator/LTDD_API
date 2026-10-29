namespace tran_nguyen_vi_khang_api.DTOs.Auth;

/// <summary>
/// Payload to verify login OTP and obtain application JWT.
/// </summary>
public class VerifyLoginOtpDto
{
    public string Email { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
}

