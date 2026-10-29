using tran_nguyen_vi_khang_api.Models;

namespace tran_nguyen_vi_khang_api.Services.Interfaces;

public interface IJwtTokenService
{
    /// <summary>
    /// Generates an application-specific JWT bearer token for the authenticated user.
    /// </summary>
    /// <param name="user">The application User entity.</param>
    /// <param name="expiresAt">Outputs the UTC expiration timestamp.</param>
    /// <returns>Signed JWT string.</returns>
    string GenerateJwtToken(User user, out DateTime expiresAt);

    /// <summary>
    /// Generates a dedicated JWT bearer token for an enrolled Child Device.
    /// </summary>
    /// <param name="device">The enrolled Device entity.</param>
    /// <param name="child">The linked Child entity.</param>
    /// <param name="expiresAt">Outputs the UTC expiration timestamp.</param>
    /// <returns>Signed JWT string with Role = ChildDevice.</returns>
    string GenerateDeviceToken(Device device, Child child, out DateTime expiresAt);
}

