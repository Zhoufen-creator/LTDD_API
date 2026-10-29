using Google.Apis.Auth;
using tran_nguyen_vi_khang_api.Services.Interfaces;

namespace tran_nguyen_vi_khang_api.Services;

public class GoogleAuthService : IGoogleAuthService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleAuthService> _logger;

    public GoogleAuthService(IConfiguration configuration, ILogger<GoogleAuthService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Validates the Google ID token against Google's public signing keys and audience.
    /// In Google Sign-In for Flutter, the mobile client specifies serverClientId = (Web Client ID),
    /// so the issued ID token contains the Web Client ID as its audience ('aud').
    /// </summary>
    public async Task<GoogleJsonWebSignature.Payload?> ValidateIdTokenAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            _logger.LogWarning("Google token validation skipped: idToken is empty.");
            return null;
        }

        var webClientId = _configuration["Authentication:Google:ClientId"];
        var additionalAudiences = _configuration.GetSection("Authentication:Google:AdditionalAudiences").Get<string[]>();

        var validAudiences = new List<string>();
        if (!string.IsNullOrWhiteSpace(webClientId))
        {
            validAudiences.Add(webClientId.Trim());
        }

        if (additionalAudiences != null && additionalAudiences.Length > 0)
        {
            validAudiences.AddRange(additionalAudiences.Where(a => !string.IsNullOrWhiteSpace(a)));
        }

        var settings = new GoogleJsonWebSignature.ValidationSettings();

        if (validAudiences.Count > 0)
        {
            settings.Audience = validAudiences;
        }
        else
        {
            _logger.LogWarning("No valid Google Client ID configured in 'Authentication:Google:ClientId'.");
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            return payload;
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "Google ID token validation failed: token is invalid or audience does not match.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred during Google ID token validation.");
            return null;
        }
    }
}
