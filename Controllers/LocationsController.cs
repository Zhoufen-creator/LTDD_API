using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using tran_nguyen_vi_khang_api.DTOs.Common;
using tran_nguyen_vi_khang_api.DTOs.Location;
using tran_nguyen_vi_khang_api.Services.Interfaces;

namespace tran_nguyen_vi_khang_api.Controllers;

/// <summary>
/// API endpoints for GPS Location synchronization and queries.
/// Enforces strict role separation: ChildDevice pushes location coordinates, Parent/Admin reads location.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class LocationsController : ControllerBase
{
    private readonly ILocationService _locationService;

    public LocationsController(ILocationService locationService)
    {
        _locationService = locationService;
    }

    private int CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private bool IsAdmin => User.IsInRole("Admin");

    /// <summary>
    /// Records GPS location coordinates for an enrolled child device.
    /// Strictly restricted to authenticated ChildDevice tokens. DeviceId and ChildId are derived from JWT claims.
    /// </summary>
    /// <param name="dto">Location coordinates and optional accuracy/recordedAt.</param>
    /// <response code="200">Location successfully recorded.</response>
    /// <response code="400">Invalid coordinates, negative accuracy, future timestamp, or device mismatch/inactive.</response>
    /// <response code="401">Unauthorized: Missing or invalid JWT bearer token.</response>
    /// <response code="403">Forbidden: Caller is not an authenticated ChildDevice.</response>
    [HttpPost]
    [Authorize(Roles = "ChildDevice")]
    [ProducesResponseType(typeof(DeviceLocationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DeviceLocationResponseDto>> RecordLocation([FromBody] CreateLocationDto dto)
    {
        var deviceIdClaim = User.FindFirstValue("DeviceId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        var childIdClaim = User.FindFirstValue("ChildId");

        if (!int.TryParse(deviceIdClaim, out var deviceId) || deviceId <= 0 ||
            !int.TryParse(childIdClaim, out var childId) || childId <= 0)
        {
            return Forbid();
        }

        var result = await _locationService.RecordLocationAsync(deviceId, childId, dto);

        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(new { message = result.ErrorMessage }),
                _ => BadRequest(new { message = result.ErrorMessage })
            };
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Retrieves the latest recorded location for a specific child.
    /// Strictly restricted to the child's parent or Admin.
    /// Returns 200 OK with JSON null if no locations have been recorded yet.
    /// </summary>
    /// <param name="childId">The target child ID.</param>
    /// <response code="200">Latest location returned, or null if no location records exist.</response>
    /// <response code="401">Unauthorized: Missing or invalid JWT bearer token.</response>
    /// <response code="403">Forbidden: Caller is not a Parent or Admin.</response>
    /// <response code="404">Child was not found or does not belong to the calling parent.</response>
    [HttpGet("child/{childId:int}")]
    [Authorize(Roles = "Parent,Admin")]
    [ProducesResponseType(typeof(DeviceLocationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceLocationResponseDto?>> GetLatestByChild(int childId)
    {
        var result = await _locationService.GetLatestLocationAsync(childId, CurrentUserId, IsAdmin);

        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(new { message = result.ErrorMessage }),
                _ => BadRequest(new { message = result.ErrorMessage })
            };
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Retrieves location history for a specific child.
    /// Strictly restricted to the child's parent or Admin.
    /// </summary>
    /// <param name="childId">The target child ID.</param>
    /// <param name="limit">Max number of entries to return (default 50, max 200).</param>
    /// <response code="200">Location history returned.</response>
    /// <response code="401">Unauthorized: Missing or invalid JWT bearer token.</response>
    /// <response code="403">Forbidden: Caller is not a Parent or Admin.</response>
    /// <response code="404">Child was not found or does not belong to the calling parent.</response>
    [HttpGet("child/{childId:int}/history")]
    [Authorize(Roles = "Parent,Admin")]
    [ProducesResponseType(typeof(IEnumerable<DeviceLocationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<DeviceLocationResponseDto>>> GetHistoryByChild(int childId, [FromQuery] int limit = 50)
    {
        var result = await _locationService.GetLocationHistoryAsync(childId, CurrentUserId, IsAdmin, limit);

        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(new { message = result.ErrorMessage }),
                _ => BadRequest(new { message = result.ErrorMessage })
            };
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Creates a new location-refresh request for a child.
    /// Strictly restricted to Parent or Admin.
    /// </summary>
    [HttpPost("refresh-requests")]
    [Authorize(Roles = "Parent,Admin")]
    [ProducesResponseType(typeof(LocationRefreshRequestResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LocationRefreshRequestResponseDto>> CreateRefreshRequest([FromBody] CreateLocationRefreshRequestDto dto)
    {
        var result = await _locationService.CreateRefreshRequestAsync(CurrentUserId, IsAdmin, dto);
        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(new { message = result.ErrorMessage }),
                _ => BadRequest(new { message = result.ErrorMessage })
            };
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Retrieves status of a specific location-refresh request.
    /// Strictly restricted to Parent or Admin.
    /// </summary>
    [HttpGet("refresh-requests/{id:int}")]
    [Authorize(Roles = "Parent,Admin")]
    [ProducesResponseType(typeof(LocationRefreshRequestResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LocationRefreshRequestResponseDto>> GetRefreshRequestStatus(int id)
    {
        var result = await _locationService.GetRefreshRequestStatusAsync(CurrentUserId, IsAdmin, id);
        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(new { message = result.ErrorMessage }),
                _ => BadRequest(new { message = result.ErrorMessage })
            };
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Checks for any pending location-refresh request assigned to this child device.
    /// Strictly restricted to authenticated ChildDevice.
    /// </summary>
    [HttpGet("refresh-requests/pending")]
    [Authorize(Roles = "ChildDevice")]
    [ProducesResponseType(typeof(LocationRefreshRequestResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LocationRefreshRequestResponseDto?>> GetPendingRefreshRequest()
    {
        var deviceIdClaim = User.FindFirstValue("DeviceId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(deviceIdClaim, out var deviceId) || deviceId <= 0)
        {
            return Forbid();
        }

        var result = await _locationService.GetPendingRefreshRequestForDeviceAsync(deviceId);
        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        if (result.Data == null)
        {
            return NoContent();
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Completes or marks failure for a location-refresh request.
    /// Strictly restricted to authenticated ChildDevice.
    /// </summary>
    [HttpPost("refresh-requests/{id:int}/complete")]
    [Authorize(Roles = "ChildDevice")]
    [ProducesResponseType(typeof(LocationRefreshRequestResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LocationRefreshRequestResponseDto>> CompleteRefreshRequest(int id, [FromBody] CompleteRefreshRequestDto dto)
    {
        var deviceIdClaim = User.FindFirstValue("DeviceId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(deviceIdClaim, out var deviceId) || deviceId <= 0)
        {
            return Forbid();
        }

        var result = await _locationService.CompleteRefreshRequestAsync(deviceId, id, dto);
        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(new { message = result.ErrorMessage }),
                _ => BadRequest(new { message = result.ErrorMessage })
            };
        }

        return Ok(result.Data);
    }
}

