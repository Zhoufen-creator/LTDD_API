using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using tran_nguyen_vi_khang_api.DTOs.Activity;
using tran_nguyen_vi_khang_api.DTOs.Common;
using tran_nguyen_vi_khang_api.Services.Interfaces;

namespace tran_nguyen_vi_khang_api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ActivitiesController : ControllerBase
{
    private readonly IActivityService _activityService;

    public ActivitiesController(IActivityService activityService)
    {
        _activityService = activityService;
    }

    /// <summary>
    /// Synchronizes a batch of child device activity events with idempotent deduplication.
    /// Restricted strictly to authenticated ChildDevice tokens.
    /// </summary>
    /// <response code="200">Batch synchronized successfully with deduplication stats.</response>
    /// <response code="400">Invalid batch payload.</response>
    /// <response code="401">Unauthorized: Missing or invalid JWT bearer token.</response>
    /// <response code="403">Forbidden: Device or child claim invalid.</response>
    [HttpPost("batch")]
    [Authorize(Roles = "ChildDevice")]
    [ProducesResponseType(typeof(BatchActivitySyncResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BatchActivitySyncResponseDto>> SyncBatchActivities([FromBody] BatchActivitySyncDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var deviceIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var childIdClaim = User.FindFirstValue("ChildId");

        if (!int.TryParse(deviceIdClaim, out var deviceId) || deviceId <= 0 ||
            !int.TryParse(childIdClaim, out var childId) || childId <= 0)
        {
            return Forbid();
        }

        var result = await _activityService.SyncBatchActivitiesAsync(deviceId, childId, dto);

        return result.ErrorType switch
        {
            ServiceErrorType.None => Ok(result.Data),
            ServiceErrorType.Forbidden => Forbid(),
            ServiceErrorType.BadRequest => BadRequest(new { message = result.ErrorMessage }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.ErrorMessage })
        };
    }

    /// <summary>
    /// Retrieves paginated activity history and usage summary for a specific child.
    /// Restricted to authenticated Parents (verifying ownership) or Admins.
    /// </summary>
    /// <response code="200">Paginated activity history and usage summary.</response>
    /// <response code="401">Unauthorized.</response>
    /// <response code="403">Forbidden: Caller is not the parent of this child.</response>
    /// <response code="404">Child record not found.</response>
    [HttpGet("child/{childId:int}")]
    [Authorize(Roles = "Parent,Admin")]
    [ProducesResponseType(typeof(ChildActivityHistoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChildActivityHistoryResponseDto>> GetChildActivities(
        int childId,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? eventType = null,
        [FromQuery] string? packageName = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var currentUserIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(currentUserIdClaim, out var currentUserId) || currentUserId <= 0)
        {
            return Unauthorized();
        }

        var isAdmin = User.IsInRole("Admin");

        var result = await _activityService.GetChildActivityHistoryAsync(
            childId,
            currentUserId,
            isAdmin,
            fromDate,
            toDate,
            eventType,
            packageName,
            page,
            pageSize);

        return result.ErrorType switch
        {
            ServiceErrorType.None => Ok(result.Data),
            ServiceErrorType.NotFound => NotFound(new { message = result.ErrorMessage }),
            ServiceErrorType.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = result.ErrorMessage }),
            ServiceErrorType.BadRequest => BadRequest(new { message = result.ErrorMessage }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.ErrorMessage })
        };
    }
}

