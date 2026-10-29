namespace tran_nguyen_vi_khang_api.DTOs.Auth;

/// <summary>
/// Payload to request resending a login OTP for a pending login session.
/// </summary>
public class ResendLoginOtpDto
{
    public string Email { get; set; } = string.Empty;
}

