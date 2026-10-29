using tran_nguyen_vi_khang_api.DTOs.Auth;
using tran_nguyen_vi_khang_api.DTOs.Common;

namespace tran_nguyen_vi_khang_api.Services.Interfaces;

public interface IAuthService
{
    /// <summary>
    /// Authenticates a user using their Google ID token, ensures a corresponding User entity
    /// exists in SQL Server, and returns an application-specific JWT.
    /// </summary>
    Task<ServiceResult<AuthResponseDto>> SignInWithGoogleAsync(GoogleSignInRequestDto request);

    /// <summary>
    /// Generates and sends a 6-digit numeric OTP for email registration.
    /// </summary>
    Task<ServiceResult<bool>> SendRegistrationOtpAsync(SendOtpRequestDto request);

    /// <summary>
    /// Validates submitted OTP and creates a new User and EmailOtp record in SQL Server atomically.
    /// </summary>
    Task<ServiceResult<AuthResponseDto>> RegisterWithOtpAsync(RegisterWithOtpDto request);

    /// <summary>
    /// Validates email and password, then generates and sends an OTP for login verification.
    /// Full application JWT is NOT issued until OTP is verified.
    /// </summary>
    Task<ServiceResult<LoginResponseDto>> LoginAsync(LoginRequestDto request);

    /// <summary>
    /// Validates submitted login OTP and issues the full application JWT token.
    /// </summary>
    Task<ServiceResult<AuthResponseDto>> VerifyLoginOtpAsync(VerifyLoginOtpDto request);

    /// <summary>
    /// Resends a new login OTP if within a valid pending login session and rate limits allow.
    /// </summary>
    Task<ServiceResult<bool>> ResendLoginOtpAsync(ResendLoginOtpDto request);
}

