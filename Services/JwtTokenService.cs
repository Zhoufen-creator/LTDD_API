using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using tran_nguyen_vi_khang_api.Models;
using tran_nguyen_vi_khang_api.Services.Interfaces;

namespace tran_nguyen_vi_khang_api.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateJwtToken(User user, out DateTime expiresAt)
    {
        var secret = _configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException("Critical configuration missing: 'Jwt:Secret' (or 'Jwt__Secret') is not configured or is shorter than 32 bytes.");
        }

        var issuer = _configuration["Jwt:Issuer"];
        if (string.IsNullOrWhiteSpace(issuer))
        {
            throw new InvalidOperationException("Critical configuration missing: 'Jwt:Issuer' (or 'Jwt__Issuer') is not configured.");
        }

        var audience = _configuration["Jwt:Audience"];
        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException("Critical configuration missing: 'Jwt:Audience' (or 'Jwt__Audience') is not configured.");
        }

        if (!int.TryParse(_configuration["Jwt:ExpiryInDays"], out var expiryDays) || expiryDays <= 0)
        {
            expiryDays = 7;
        }

        expiresAt = DateTime.UtcNow.AddDays(expiryDays);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, string.IsNullOrWhiteSpace(user.Role) ? "Parent" : user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }

    public string GenerateDeviceToken(Device device, Child child, out DateTime expiresAt)
    {
        var secret = _configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException("Critical configuration missing: 'Jwt:Secret' (or 'Jwt__Secret') is not configured or is shorter than 32 bytes.");
        }

        var issuer = _configuration["Jwt:Issuer"];
        if (string.IsNullOrWhiteSpace(issuer))
        {
            throw new InvalidOperationException("Critical configuration missing: 'Jwt:Issuer' (or 'Jwt__Issuer') is not configured.");
        }

        var audience = _configuration["Jwt:Audience"];
        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException("Critical configuration missing: 'Jwt:Audience' (or 'Jwt__Audience') is not configured.");
        }

        if (!int.TryParse(_configuration["Jwt:DeviceExpiryInDays"], out var expiryDays) || expiryDays <= 0)
        {
            expiryDays = 30;
        }

        expiresAt = DateTime.UtcNow.AddDays(expiryDays);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, device.Id.ToString()),
            new(ClaimTypes.Name, device.DeviceName),
            new(ClaimTypes.Role, "ChildDevice"),
            new("DeviceId", device.Id.ToString()),
            new("ChildId", child.Id.ToString()),
            new("ParentId", child.ParentId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Sub, device.Id.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}
