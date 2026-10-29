namespace tran_nguyen_vi_khang_api.DTOs.Auth;

/// <summary>
/// Payload for standard email and password login.
/// </summary>
public class LoginRequestDto
{
    /// <summary>
    /// Registered user email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User account password.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}

