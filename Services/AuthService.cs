using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using tran_nguyen_vi_khang_api.Data;
using tran_nguyen_vi_khang_api.DTOs.Auth;
using tran_nguyen_vi_khang_api.DTOs.Common;
using tran_nguyen_vi_khang_api.Models;
using tran_nguyen_vi_khang_api.Services.Interfaces;

namespace tran_nguyen_vi_khang_api.Services;

public class AuthService : IAuthService
{
    private readonly IGoogleAuthService _googleAuthService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IEmailSenderService _emailSenderService;
    private readonly IMemoryCache _cache;
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IGoogleAuthService googleAuthService,
        IJwtTokenService jwtTokenService,
        IEmailSenderService emailSenderService,
        IMemoryCache cache,
        AppDbContext context,
        IWebHostEnvironment environment,
        ILogger<AuthService> logger)
    {
        _googleAuthService = googleAuthService;
        _jwtTokenService = jwtTokenService;
        _emailSenderService = emailSenderService;
        _cache = cache;
        _context = context;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// Generates and sends a 6-digit numeric OTP for email registration.
    /// Uses In-Memory Cache with expiration to avoid premature DB insertion and foreign key conflicts.
    /// </summary>
    public async Task<ServiceResult<bool>> SendRegistrationOtpAsync(SendOtpRequestDto request)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            return ServiceResult<bool>.BadRequest("Email là bắt buộc.");
        }

        // 1. Verify that email is not already registered
        var userExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == email);
        if (userExists)
        {
            _logger.LogWarning("Registration OTP requested for an already existing email: {MaskedEmail}", MaskEmail(email));
            return ServiceResult<bool>.BadRequest("Email này đã được đăng ký trong hệ thống. Vui lòng đăng nhập hoặc sử dụng email khác.");
        }

        // 2. Rate limiting check (minimum 60 seconds between resend requests)
        var cacheKey = $"reg_otp_{email}";
        if (_cache.TryGetValue(cacheKey, out OtpCacheItem? existingItem) && existingItem != null)
        {
            var secondsSinceLastSend = (DateTime.UtcNow - existingItem.CreatedAt).TotalSeconds;
            if (secondsSinceLastSend < 60)
            {
                var waitSeconds = (int)(60 - secondsSinceLastSend);
                return ServiceResult<bool>.BadRequest($"Vui lòng đợi {waitSeconds} giây trước khi yêu cầu gửi lại mã OTP mới.");
            }
        }

        // 3. Generate 6-digit numeric OTP cryptographically
        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var otpHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(otp)));

        // 4. Store OTP in Memory Cache with 5 minutes sliding/absolute expiration
        var cacheItem = new OtpCacheItem
        {
            Hash = otpHash,
            CreatedAt = DateTime.UtcNow,
            Attempts = 0
        };

        _cache.Set(cacheKey, cacheItem, TimeSpan.FromMinutes(5));

        // 5. Send OTP via email service
        var emailSent = await _emailSenderService.SendOtpEmailAsync(email, otp);
        if (!emailSent)
        {
            _cache.Remove(cacheKey);
            return ServiceResult<bool>.InternalError("Không thể gửi email chứa mã OTP. Dịch vụ email hiện không khả dụng, vui lòng kiểm tra lại cấu hình hoặc thử lại sau.");
        }

        _logger.LogInformation("Registration OTP successfully generated and dispatched for {MaskedEmail}.", MaskEmail(email));
        return ServiceResult<bool>.Success(true);
    }

    /// <summary>
    /// Validates submitted OTP and creates User and EmailOtp records atomically in SQL Server.
    /// </summary>
    public async Task<ServiceResult<AuthResponseDto>> RegisterWithOtpAsync(RegisterWithOtpDto request)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        var otp = request.Otp?.Trim();
        var fullName = request.FullName?.Trim();
        var password = request.Password;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp) ||
            string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(password))
        {
            return ServiceResult<AuthResponseDto>.BadRequest("Vui lòng điền đầy đủ tất cả các trường thông tin.");
        }

        // 1. Verify email uniqueness
        var userExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == email);
        if (userExists)
        {
            return ServiceResult<AuthResponseDto>.BadRequest("Email này đã được đăng ký trong hệ thống.");
        }

        // 2. Validate OTP from cache
        var cacheKey = $"reg_otp_{email}";
        if (!_cache.TryGetValue(cacheKey, out OtpCacheItem? cachedItem) || cachedItem == null)
        {
            return ServiceResult<AuthResponseDto>.BadRequest("Mã OTP đã hết hạn hoặc không tồn tại. Vui lòng yêu cầu gửi lại mã mới.");
        }

        // Check maximum failed attempts
        if (cachedItem.Attempts >= 5)
        {
            _cache.Remove(cacheKey);
            return ServiceResult<AuthResponseDto>.BadRequest("Bạn đã nhập sai mã OTP quá 5 lần. Mã đã bị hủy, vui lòng yêu cầu mã mới.");
        }

        // 3. Constant-time hash verification
        var inputHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(otp)));
        var isMatch = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(inputHash), Encoding.UTF8.GetBytes(cachedItem.Hash));

        // In Development environment, permit standard fallback OTP "123456" for automated testing when SMTP is not configured
        if (!isMatch && _environment.IsDevelopment() && otp == "123456")
        {
            isMatch = true;
        }

        if (!isMatch)
        {
            cachedItem.Attempts++;
            var remaining = 5 - cachedItem.Attempts;
            return ServiceResult<AuthResponseDto>.BadRequest($"Mã OTP không chính xác. Bạn còn {remaining} lần thử.");
        }

        // 4. Hash password with PBKDF2
        var passwordHash = HashPassword(password);

        // 5. Atomic SQL Server Transaction: Create User + EmailOtp
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var user = new User
            {
                FullName = fullName.Length > 100 ? fullName[..100] : fullName,
                Email = email,
                PasswordHash = passwordHash,
                GoogleSubjectId = null,
                Role = "Parent",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Insert EmailOtp record referencing the newly generated UserId
            var emailOtpRecord = new EmailOtp
            {
                UserId = user.Id,
                OtpHash = cachedItem.Hash,
                ExpiresAt = cachedItem.CreatedAt.AddMinutes(5),
                IsUsed = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.EmailOtps.Add(emailOtpRecord);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            // Remove OTP from cache upon successful registration
            _cache.Remove(cacheKey);
            _logger.LogInformation("New user registered successfully via Email OTP with ID: {UserId}.", user.Id);

            // 6. Issue application JWT token
            var jwtToken = _jwtTokenService.GenerateJwtToken(user, out var expiresAt);

            return ServiceResult<AuthResponseDto>.Success(new AuthResponseDto
            {
                Token = jwtToken,
                ExpiresAt = expiresAt,
                User = new AuthUserDto
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    Role = user.Role
                }
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Database transaction failed while registering user for {MaskedEmail}.", MaskEmail(email));
            return ServiceResult<AuthResponseDto>.InternalError("Lỗi hệ thống khi tạo tài khoản. Vui lòng thử lại sau.");
        }
    }

    /// <summary>
    /// Validates email and password, then generates and dispatches an OTP for two-factor authentication.
    /// Full application JWT is NOT issued until OTP is verified.
    /// </summary>
    public async Task<ServiceResult<LoginResponseDto>> LoginAsync(LoginRequestDto request)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        var password = request.Password;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return ServiceResult<LoginResponseDto>.BadRequest("Vui lòng nhập đầy đủ email và mật khẩu.");
        }

        // 1. Look up user by email
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
        if (user == null || !user.IsActive || string.IsNullOrEmpty(user.PasswordHash))
        {
            // Generic error message to prevent account enumeration
            return ServiceResult<LoginResponseDto>.BadRequest("Email hoặc mật khẩu không chính xác.");
        }

        // 2. Constant-time password verification using existing PBKDF2 hash scheme
        if (!VerifyPassword(password, user.PasswordHash))
        {
            return ServiceResult<LoginResponseDto>.BadRequest("Email hoặc mật khẩu không chính xác.");
        }

        // 3. Rate limiting check (minimum 60 seconds between resend requests)
        var cacheKey = $"login_otp_{email}";
        if (_cache.TryGetValue(cacheKey, out OtpCacheItem? existingItem) && existingItem != null)
        {
            var secondsSinceLastSend = (DateTime.UtcNow - existingItem.CreatedAt).TotalSeconds;
            if (secondsSinceLastSend < 60)
            {
                var waitSeconds = (int)(60 - secondsSinceLastSend);
                return ServiceResult<LoginResponseDto>.BadRequest($"Vui lòng đợi {waitSeconds} giây trước khi yêu cầu gửi lại mã OTP mới.");
            }
        }

        // 4. Cryptographically generate 6-digit numeric OTP
        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var otpHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(otp)));

        // 5. Store OTP in Memory Cache with 5 minutes sliding/absolute expiration
        var cacheItem = new OtpCacheItem
        {
            Hash = otpHash,
            CreatedAt = DateTime.UtcNow,
            Attempts = 0,
            UserId = user.Id
        };

        _cache.Set(cacheKey, cacheItem, TimeSpan.FromMinutes(5));

        // 6. Send OTP via email service
        var emailSent = await _emailSenderService.SendOtpEmailAsync(email, otp);
        if (!emailSent)
        {
            _cache.Remove(cacheKey);
            return ServiceResult<LoginResponseDto>.InternalError("Không thể gửi email chứa mã OTP. Dịch vụ email hiện không khả dụng, vui lòng kiểm tra lại cấu hình hoặc thử lại sau.");
        }

        _logger.LogInformation("Login OTP successfully generated and dispatched for {MaskedEmail}.", MaskEmail(email));
        return ServiceResult<LoginResponseDto>.Success(new LoginResponseDto
        {
            Email = email,
            Message = "Mã xác thực OTP đã được gửi đến email của bạn.",
            RequiresOtp = true
        });
    }

    /// <summary>
    /// Resends a new login OTP if within a valid pending login session and rate limits allow.
    /// </summary>
    public async Task<ServiceResult<bool>> ResendLoginOtpAsync(ResendLoginOtpDto request)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            return ServiceResult<bool>.BadRequest("Email là bắt buộc.");
        }

        var cacheKey = $"login_otp_{email}";
        if (!_cache.TryGetValue(cacheKey, out OtpCacheItem? existingItem) || existingItem == null)
        {
            return ServiceResult<bool>.BadRequest("Phiên đăng nhập đã hết hạn hoặc không tồn tại. Vui lòng đăng nhập lại.");
        }

        var secondsSinceLastSend = (DateTime.UtcNow - existingItem.CreatedAt).TotalSeconds;
        if (secondsSinceLastSend < 60)
        {
            var waitSeconds = (int)(60 - secondsSinceLastSend);
            return ServiceResult<bool>.BadRequest($"Vui lòng đợi {waitSeconds} giây trước khi yêu cầu gửi lại mã OTP.");
        }

        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var otpHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(otp)));

        existingItem.Hash = otpHash;
        existingItem.CreatedAt = DateTime.UtcNow;
        existingItem.Attempts = 0;

        _cache.Set(cacheKey, existingItem, TimeSpan.FromMinutes(5));

        var emailSent = await _emailSenderService.SendOtpEmailAsync(email, otp);
        if (!emailSent)
        {
            _cache.Remove(cacheKey);
            return ServiceResult<bool>.InternalError("Không thể gửi email chứa mã OTP. Dịch vụ email hiện không khả dụng, vui lòng kiểm tra lại cấu hình hoặc thử lại sau.");
        }

        _logger.LogInformation("Login OTP successfully resent for {MaskedEmail}.", MaskEmail(email));
        return ServiceResult<bool>.Success(true);
    }

    /// <summary>
    /// Validates submitted login OTP and issues the full application JWT token.
    /// </summary>
    public async Task<ServiceResult<AuthResponseDto>> VerifyLoginOtpAsync(VerifyLoginOtpDto request)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        var otp = request.Otp?.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp))
        {
            return ServiceResult<AuthResponseDto>.BadRequest("Email và mã OTP là bắt buộc.");
        }

        var cacheKey = $"login_otp_{email}";
        if (!_cache.TryGetValue(cacheKey, out OtpCacheItem? cachedItem) || cachedItem == null)
        {
            return ServiceResult<AuthResponseDto>.BadRequest("Mã OTP đã hết hạn hoặc không tồn tại. Vui lòng đăng nhập lại.");
        }

        if (cachedItem.Attempts >= 5)
        {
            _cache.Remove(cacheKey);
            return ServiceResult<AuthResponseDto>.BadRequest("Bạn đã nhập sai mã OTP quá 5 lần. Mã đã bị hủy, vui lòng đăng nhập lại.");
        }

        // Constant-time hash comparison
        var inputHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(otp)));
        var isMatch = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(inputHash), Encoding.UTF8.GetBytes(cachedItem.Hash));

        // In Development environment, permit standard fallback OTP "123456" for automated testing when SMTP is not configured
        if (!isMatch && _environment.IsDevelopment() && otp == "123456")
        {
            isMatch = true;
        }

        if (!isMatch)
        {
            cachedItem.Attempts++;
            var remaining = 5 - cachedItem.Attempts;
            return ServiceResult<AuthResponseDto>.BadRequest($"Mã OTP không chính xác. Bạn còn {remaining} lần thử.");
        }

        // OTP is valid - consume it immediately
        _cache.Remove(cacheKey);

        var user = cachedItem.UserId.HasValue
            ? await _context.Users.FindAsync(cachedItem.UserId.Value)
            : await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        if (user == null || !user.IsActive)
        {
            return ServiceResult<AuthResponseDto>.BadRequest("Tài khoản người dùng không tồn tại hoặc đã bị vô hiệu hóa.");
        }

        // Record EmailOtp in SQL Server for audit trail
        try
        {
            var emailOtpRecord = new EmailOtp
            {
                UserId = user.Id,
                OtpHash = cachedItem.Hash,
                ExpiresAt = cachedItem.CreatedAt.AddMinutes(5),
                IsUsed = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.EmailOtps.Add(emailOtpRecord);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record login EmailOtp audit row for user {UserId}", user.Id);
        }

        // Issue application-specific JWT token
        var jwtToken = _jwtTokenService.GenerateJwtToken(user, out var expiresAt);

        _logger.LogInformation("User ID {UserId} logged in successfully via Email OTP.", user.Id);

        return ServiceResult<AuthResponseDto>.Success(new AuthResponseDto
        {
            Token = jwtToken,
            ExpiresAt = expiresAt,
            User = new AuthUserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role
            }
        });
    }

    public async Task<ServiceResult<AuthResponseDto>> SignInWithGoogleAsync(GoogleSignInRequestDto request)
    {
        var idToken = request.IdToken?.Trim();
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return ServiceResult<AuthResponseDto>.BadRequest("Google ID token is required.");
        }

        // 1. Validate the Google ID token cryptographically (signature, audience, issuer, expiration)
        var payload = await _googleAuthService.ValidateIdTokenAsync(idToken);
        if (payload == null)
        {
            return ServiceResult<AuthResponseDto>.BadRequest("Google authentication failed: Invalid ID token, signature, or audience mismatch.");
        }

        if (string.IsNullOrWhiteSpace(payload.Email))
        {
            return ServiceResult<AuthResponseDto>.BadRequest("Google token payload did not contain an email address.");
        }

        // 2. Strict verification: Ensure Google has verified ownership of the email address
        if (!payload.EmailVerified)
        {
            return ServiceResult<AuthResponseDto>.BadRequest("Google authentication failed: Email address has not been verified by Google.");
        }

        var email = payload.Email.Trim().ToLowerInvariant();
        var googleSub = payload.Subject;

        User? user;

        // 3. Primary lookup: Match by immutable Google Subject ID ('sub' claim)
        user = await _context.Users
            .FirstOrDefaultAsync(u => u.GoogleSubjectId == googleSub);

        if (user != null)
        {
            if (!user.IsActive)
            {
                return ServiceResult<AuthResponseDto>.BadRequest("This user account has been deactivated.");
            }

            var modified = false;

            // Keep email synchronized if updated on Google, provided no collision
            if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                var emailCollision = await _context.Users.AnyAsync(u => u.Id != user.Id && u.Email.ToLower() == email);
                if (!emailCollision)
                {
                    user.Email = email;
                    modified = true;
                }
            }

            // Update FullName if currently missing
            if (string.IsNullOrWhiteSpace(user.FullName) && !string.IsNullOrWhiteSpace(payload.Name))
            {
                user.FullName = payload.Name.Trim();
                modified = true;
            }

            if (modified)
            {
                await _context.SaveChangesAsync();
            }
        }
        else
        {
            // 4. Secondary lookup: Check if account exists with this email
            var existingUserByEmail = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

            if (existingUserByEmail != null)
            {
                if (!existingUserByEmail.IsActive)
                {
                    return ServiceResult<AuthResponseDto>.BadRequest("This user account has been deactivated.");
                }

                // Security check: If already linked to a DIFFERENT Google account, reject
                if (!string.IsNullOrEmpty(existingUserByEmail.GoogleSubjectId) && existingUserByEmail.GoogleSubjectId != googleSub)
                {
                    return ServiceResult<AuthResponseDto>.BadRequest("This email address is already linked to another Google account.");
                }

                // Link GoogleSubjectId because email ownership was cryptographically confirmed by Google
                existingUserByEmail.GoogleSubjectId = googleSub;
                if (string.IsNullOrWhiteSpace(existingUserByEmail.FullName) && !string.IsNullOrWhiteSpace(payload.Name))
                {
                    existingUserByEmail.FullName = payload.Name.Trim();
                }

                await _context.SaveChangesAsync();
                user = existingUserByEmail;
                _logger.LogInformation("Existing user account ID {UserId} successfully linked to Google Subject ID.", user.Id);
            }
            else
            {
                // 5. Account does not exist: Provision new User record
                var displayName = !string.IsNullOrWhiteSpace(payload.Name)
                    ? payload.Name.Trim()
                    : email.Split('@')[0];

                if (displayName.Length > 100)
                {
                    displayName = displayName[..100];
                }

                user = new User
                {
                    FullName = displayName,
                    Email = email,
                    GoogleSubjectId = googleSub,
                    PasswordHash = null,
                    Role = "Parent",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                _logger.LogInformation("New user created via Google Sign-In with ID: {UserId}.", user.Id);
            }
        }

        // 6. Issue application-specific JWT token
        var jwtToken = _jwtTokenService.GenerateJwtToken(user, out var expiresAt);

        var response = new AuthResponseDto
        {
            Token = jwtToken,
            ExpiresAt = expiresAt,
            User = new AuthUserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role
            }
        };

        return ServiceResult<AuthResponseDto>.Success(response);
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations: 100000,
            HashAlgorithmName.SHA256,
            outputLength: 32);

        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string storedPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(storedPasswordHash) || !storedPasswordHash.Contains(':'))
        {
            return false;
        }

        var parts = storedPasswordHash.Split(':');
        if (parts.Length != 2) return false;

        try
        {
            var salt = Convert.FromBase64String(parts[0]);
            var expectedHash = Convert.FromBase64String(parts[1]);

            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations: 100000,
                HashAlgorithmName.SHA256,
                outputLength: 32);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) return "***";
        var parts = email.Split('@');
        var name = parts[0];
        var domain = parts[1];
        var maskedName = name.Length <= 2 ? name[0] + "*" : name[0] + new string('*', name.Length - 2) + name[^1];
        return $"{maskedName}@{domain}";
    }

    private class OtpCacheItem
    {
        public string Hash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int Attempts { get; set; }
        public int? UserId { get; set; }
    }
}
