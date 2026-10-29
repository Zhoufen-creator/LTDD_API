using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using tran_nguyen_vi_khang_api.Data;
using tran_nguyen_vi_khang_api.DTOs.Auth;
using tran_nguyen_vi_khang_api.DTOs.Common;
using tran_nguyen_vi_khang_api.Services.Interfaces;

namespace tran_nguyen_vi_khang_api.Controllers;

/// <summary>
/// Endpoints for handling user authentication, including Google Sign-In and application JWT issuance.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly AppDbContext _context;

    public AuthController(IAuthService authService, AppDbContext context)
    {
        _authService = authService;
        _context = context;
    }

    /// <summary>
    /// Authenticates a user using a Google ID token obtained from Google Sign-In on mobile/web.
    /// Creates or links the User in SQL Server and returns an application-specific JWT bearer token.
    /// </summary>
    /// <param name="request">Payload containing the Google ID token.</param>
    /// <response code="200">Google authentication successful, application JWT token issued.</response>
    /// <response code="400">Invalid ID token, expired token, or audience mismatch.</response>
    [HttpPost("google")]
    [HttpPost("google-signin")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponseDto>> GoogleSignIn([FromBody] GoogleSignInRequestDto request)
    {
        var result = await _authService.SignInWithGoogleAsync(request);

        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ServiceErrorType.BadRequest => BadRequest(new { message = result.ErrorMessage }),
                ServiceErrorType.NotFound => NotFound(new { message = result.ErrorMessage }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.ErrorMessage })
            };
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Retrieves the profile of the currently authenticated user using the application JWT token.
    /// Requires HTTP Authorization header: Bearer {token}
    /// </summary>
    /// <response code="200">Authenticated user profile retrieved successfully.</response>
    /// <response code="401">Unauthorized: Missing, invalid, or expired JWT token.</response>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(AuthUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthUserDto>> GetCurrentUser()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null || !user.IsActive)
        {
            return Unauthorized(new { message = "User not found or inactive." });
        }

        return Ok(new AuthUserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role
        });
    }

    /// <summary>
    /// Generates and sends a 6-digit numeric OTP to the specified email address for new user registration.
    /// Does not create any user record in SQL Server prior to OTP verification.
    /// </summary>
    /// <param name="request">Payload containing the target registration email address.</param>
    /// <response code="200">OTP code generated and sent successfully.</response>
    /// <response code="400">Invalid email or email already registered in system.</response>
    [HttpPost("register/send-otp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendRegistrationOtp([FromBody] SendOtpRequestDto request)
    {
        var result = await _authService.SendRegistrationOtpAsync(request);

        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ServiceErrorType.BadRequest => BadRequest(new { message = result.ErrorMessage }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.ErrorMessage })
            };
        }

        return Ok(new { message = "Mã OTP đã được gửi đến email của bạn." });
    }

    /// <summary>
    /// Validates the submitted OTP and creates a new User and EmailOtp record in SQL Server atomically.
    /// Returns application JWT bearer token upon successful registration.
    /// </summary>
    /// <param name="request">Payload containing email, OTP, fullName, and password.</param>
    /// <response code="200">User registered successfully and application JWT token issued.</response>
    /// <response code="400">Invalid OTP, expired OTP, or validation error.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponseDto>> RegisterWithOtp([FromBody] RegisterWithOtpDto request)
    {
        var result = await _authService.RegisterWithOtpAsync(request);

        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ServiceErrorType.BadRequest => BadRequest(new { message = result.ErrorMessage }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.ErrorMessage })
            };
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Authenticates a user by email and password, then triggers an OTP verification email.
    /// Does NOT issue full application JWT until OTP is verified.
    /// </summary>
    /// <param name="request">Payload containing email and password.</param>
    /// <response code="200">Credentials verified and OTP sent.</response>
    /// <response code="400">Invalid credentials or validation error.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        var result = await _authService.LoginAsync(request);

        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ServiceErrorType.BadRequest => BadRequest(new { message = result.ErrorMessage }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.ErrorMessage })
            };
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Validates the login OTP code and issues the full application JWT token.
    /// </summary>
    /// <param name="request">Payload containing email and OTP code.</param>
    /// <response code="200">OTP verified successfully and application JWT token issued.</response>
    /// <response code="400">Invalid or expired OTP.</response>
    [HttpPost("login/verify-otp")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponseDto>> VerifyLoginOtp([FromBody] VerifyLoginOtpDto request)
    {
        var result = await _authService.VerifyLoginOtpAsync(request);

        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ServiceErrorType.BadRequest => BadRequest(new { message = result.ErrorMessage }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.ErrorMessage })
            };
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Resends a new OTP code for an active pending login attempt.
    /// </summary>
    /// <param name="request">Payload containing email address.</param>
    /// <response code="200">New OTP dispatched successfully.</response>
    /// <response code="400">Rate limit exceeded or session expired.</response>
    [HttpPost("login/resend-otp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResendLoginOtp([FromBody] ResendLoginOtpDto request)
    {
        var result = await _authService.ResendLoginOtpAsync(request);

        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ServiceErrorType.BadRequest => BadRequest(new { message = result.ErrorMessage }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.ErrorMessage })
            };
        }

        return Ok(new { message = "Mã OTP mới đã được gửi đến email của bạn." });
    }
}
