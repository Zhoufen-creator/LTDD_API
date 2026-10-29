namespace tran_nguyen_vi_khang_api.DTOs.Auth;

/// <summary>
/// Intermediate response returned after valid email/password submission, requiring OTP verification.
/// </summary>
public class LoginResponseDto
{
    public string Email { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool RequiresOtp { get; set; } = true;
}

