namespace tran_nguyen_vi_khang_api.DTOs.Auth;

/// <summary>
/// Response returned upon successful authentication containing application-specific JWT and user details.
/// </summary>
public class AuthResponseDto
{
    /// <summary>
    /// Application-issued JWT token for accessing protected endpoints.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp indicating when the application JWT token expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Basic profile information of the authenticated user.
    /// </summary>
    public AuthUserDto User { get; set; } = null!;
}

public class AuthUserDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

