using System.ComponentModel.DataAnnotations;

namespace tran_nguyen_vi_khang_api.DTOs.Auth;

/// <summary>
/// Request payload containing the Google ID token issued by the Flutter Google Sign-In flow.
/// </summary>
public class GoogleSignInRequestDto
{
    /// <summary>
    /// Cryptographically signed Google ID token (JWT) returned from Google Sign-In.
    /// Note: The Flutter client should obtain this token using the Web Application Client ID as the serverClientId.
    /// </summary>
    [Required(ErrorMessage = "IdToken is required.")]
    public string IdToken { get; set; } = string.Empty;
}

