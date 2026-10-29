using Google.Apis.Auth;

namespace tran_nguyen_vi_khang_api.Services.Interfaces;

public interface IGoogleAuthService
{
    /// <summary>
    /// Validates a Google ID token cryptographically using Google's public keys
    /// and ensures the audience matches the configured Google Client ID.
    /// </summary>
    /// <param name="idToken">The Google ID token (JWT) sent by the client.</param>
    /// <returns>The validated Google token payload, or null if validation fails.</returns>
    Task<GoogleJsonWebSignature.Payload?> ValidateIdTokenAsync(string idToken);
}

