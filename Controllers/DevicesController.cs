using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using tran_nguyen_vi_khang_api.DTOs.Common;
using tran_nguyen_vi_khang_api.DTOs.Device;
using tran_nguyen_vi_khang_api.DTOs.MonitoringRule;
using tran_nguyen_vi_khang_api.Services.Interfaces;

namespace tran_nguyen_vi_khang_api.Controllers;

/// <summary>
/// API endpoints for managing Child Devices.
/// Strictly enforces JWT Authentication, Role Separation (Parent/Admin vs ChildDevice), and Parent Data Ownership.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DevicesController : ControllerBase
{
    private readonly IDeviceService _deviceService;

    public DevicesController(IDeviceService deviceService)
    {
        _deviceService = deviceService;
    }

    private int CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private bool IsAdmin => User.IsInRole("Admin");

    /// <summary>
    /// Retrieves all devices belonging to the authenticated parent's children.
    /// </summary>
    /// <param name="childId">Optional child ID filter.</param>
    /// <response code="200">List of devices successfully retrieved.</response>
    /// <response code="401">Unauthorized: Missing or invalid JWT bearer token.</response>
    /// <response code="403">Forbidden: Caller is not a Parent or Admin.</response>
    [HttpGet]
    [Authorize(Roles = "Parent,Admin")]
    [ProducesResponseType(typeof(IEnumerable<DeviceResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<DeviceResponseDto>>> GetAll([FromQuery] int? childId = null)
    {
        var devices = await _deviceService.GetAllAsync(CurrentUserId, IsAdmin, childId);
        return Ok(devices);
    }

    /// <summary>
    /// Retrieves a specific device by ID with ownership verification.
    /// </summary>
    /// <param name="id">Device ID.</param>
    /// <response code="200">Device successfully retrieved.</response>
    /// <response code="401">Unauthorized: Missing or invalid JWT bearer token.</response>
    /// <response code="403">Forbidden: Caller is not a Parent or Admin.</response>
    /// <response code="404">Device with specified ID was not found or does not belong to you.</response>
    [HttpGet("{id:int}")]
    [Authorize(Roles = "Parent,Admin")]
    [ProducesResponseType(typeof(DeviceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceResponseDto>> GetById(int id)
    {
        var device = await _deviceService.GetByIdAsync(id, CurrentUserId, IsAdmin);
        if (device == null)
        {
            return NotFound(new { message = $"Device with ID {id} was not found." });
        }

        return Ok(device);
    }

    /// <summary>
    /// Enrolls/creates a new device for a child belonging to the authenticated parent.
    /// </summary>
    /// <param name="dto">Device creation payload.</param>
    /// <response code="201">Device successfully created.</response>
    /// <response code="400">Invalid input or target child does not belong to you.</response>
    /// <response code="401">Unauthorized: Missing or invalid JWT bearer token.</response>
    /// <response code="403">Forbidden: Caller is not a Parent or Admin.</response>
    [HttpPost]
    [Authorize(Roles = "Parent,Admin")]
    [ProducesResponseType(typeof(DeviceResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DeviceResponseDto>> Create([FromBody] CreateDeviceDto dto)
    {
        var result = await _deviceService.CreateAsync(dto, CurrentUserId, IsAdmin);

        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ServiceErrorType.BadRequest => BadRequest(new { message = result.ErrorMessage }),
                ServiceErrorType.NotFound => NotFound(new { message = result.ErrorMessage }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.ErrorMessage })
            };
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    /// <summary>
    /// Updates an existing device with ownership verification.
    /// </summary>
    /// <param name="id">Device ID to update.</param>
    /// <param name="dto">Updated device details.</param>
    /// <response code="200">Device successfully updated.</response>
    /// <response code="400">Invalid input data.</response>
    /// <response code="401">Unauthorized: Missing or invalid JWT bearer token.</response>
    /// <response code="403">Forbidden: Caller is not a Parent or Admin.</response>
    /// <response code="404">Device with specified ID was not found or does not belong to you.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Parent,Admin")]
    [ProducesResponseType(typeof(DeviceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceResponseDto>> Update(int id, [FromBody] UpdateDeviceDto dto)
    {
        var result = await _deviceService.UpdateAsync(id, dto, CurrentUserId, IsAdmin);

        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(new { message = result.ErrorMessage }),
                ServiceErrorType.BadRequest => BadRequest(new { message = result.ErrorMessage }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.ErrorMessage })
            };
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Deletes a device by ID with ownership verification.
    /// </summary>
    /// <param name="id">Device ID to delete.</param>
    /// <response code="204">Device successfully deleted.</response>
    /// <response code="401">Unauthorized: Missing or invalid JWT bearer token.</response>
    /// <response code="403">Forbidden: Caller is not a Parent or Admin.</response>
    /// <response code="404">Device with specified ID was not found or does not belong to you.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Parent,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _deviceService.DeleteAsync(id, CurrentUserId, IsAdmin);

        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(new { message = result.ErrorMessage }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.ErrorMessage })
            };
        }

        return NoContent();
    }

    /// <summary>
    /// Generates a single-use QR pairing ticket for enrolling a child's device.
    /// Restricted to authenticated parents (or admin) owning the child.
    /// </summary>
    /// <param name="dto">Target child ID and optional validity duration.</param>
    /// <response code="200">Pairing ticket successfully created.</response>
    /// <response code="401">Unauthorized: Missing or invalid JWT bearer token.</response>
    /// <response code="403">Forbidden: Role is not Parent/Admin.</response>
    /// <response code="404">Child with specified ID was not found or does not belong to you.</response>
    [HttpPost("qr/pairing-ticket")]
    [Authorize(Roles = "Parent,Admin")]
    [ProducesResponseType(typeof(PairingTicketResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PairingTicketResponseDto>> CreatePairingTicket([FromBody] CreatePairingTicketDto dto)
    {
        var result = await _deviceService.CreatePairingTicketAsync(dto, CurrentUserId, IsAdmin);

        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(new { message = result.ErrorMessage }),
                ServiceErrorType.BadRequest => BadRequest(new { message = result.ErrorMessage }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = result.ErrorMessage })
            };
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Enrolls a child device using a valid single-use QR pairing ticket.
    /// This endpoint is unauthenticated, consumes the ticket atomically, and returns a ChildDevice JWT.
    /// </summary>
    /// <param name="dto">Pairing code and device metadata.</param>
    /// <response code="200">Device successfully enrolled, returns ChildDevice JWT.</response>
    /// <response code="400">Invalid pairing ticket, expired ticket, or already used ticket.</response>
    [HttpPost("qr/enroll")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(EnrollmentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EnrollmentResponseDto>> Enroll([FromBody] DeviceEnrollmentDto dto)
    {
        var result = await _deviceService.EnrollDeviceAsync(dto);

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
    /// Retrieves active monitoring rules for the enrolled child device.
    /// Restricted strictly to authenticated ChildDevice tokens.
    /// </summary>
    /// <response code="200">List of monitoring rules for this child device.</response>
    /// <response code="401">Unauthorized: Missing or invalid JWT bearer token.</response>
    /// <response code="403">Forbidden: Caller is not an authenticated ChildDevice.</response>
    [HttpGet("my-rules")]
    [Authorize(Roles = "ChildDevice")]
    [ProducesResponseType(typeof(IEnumerable<MonitoringRuleResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<MonitoringRuleResponseDto>>> GetMyRules()
    {
        var childIdClaim = User.FindFirstValue("ChildId");
        if (!int.TryParse(childIdClaim, out var childId) || childId <= 0)
        {
            return Forbid();
        }

        var result = await _deviceService.GetMyRulesAsync(childId);
        return Ok(result.Data);
    }

    /// <summary>
    /// Acknowledges rule sync/execution status from the child device.
    /// </summary>
    [HttpPost("my-rules/ack")]
    [Authorize(Roles = "ChildDevice")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AcknowledgeRules([FromBody] BatchRuleSyncAckDto dto)
    {
        var childIdClaim = User.FindFirstValue("ChildId");
        if (!int.TryParse(childIdClaim, out var childId) || childId <= 0)
        {
            return Forbid();
        }

        var result = await _deviceService.AcknowledgeRuleSyncAsync(childId, dto);
        return Ok(new { success = result.IsSuccess });
    }

    /// <summary>
    /// Updates current battery percentage and charging state for this child device.
    /// Restricted strictly to authenticated ChildDevice tokens. DeviceId is derived from JWT claims.
    /// </summary>
    [HttpPost("battery")]
    [Authorize(Roles = "ChildDevice")]
    [ProducesResponseType(typeof(DeviceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DeviceResponseDto>> UpdateBattery([FromBody] UpdateBatteryDto dto)
    {
        var deviceIdClaim = User.FindFirstValue("DeviceId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(deviceIdClaim, out var deviceId) || deviceId <= 0)
        {
            return Forbid();
        }

        var result = await _deviceService.UpdateBatteryStatusAsync(deviceId, dto);
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
    /// Creates a mode change or unlinking request from the enrolled child device.
    /// Restricted strictly to authenticated ChildDevice tokens.
    /// </summary>
    [HttpPost("mode-requests")]
    [Authorize(Roles = "ChildDevice")]
    [ProducesResponseType(typeof(DeviceModeRequestResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DeviceModeRequestResponseDto>> CreateModeRequest([FromBody] CreateModeRequestDto dto)
    {
        var deviceIdClaim = User.FindFirstValue("DeviceId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        var childIdClaim = User.FindFirstValue("ChildId");

        if (!int.TryParse(deviceIdClaim, out var deviceId) || deviceId <= 0 ||
            !int.TryParse(childIdClaim, out var childId) || childId <= 0)
        {
            return Forbid();
        }

        var result = await _deviceService.CreateModeRequestAsync(deviceId, childId, dto);
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
    /// Retrieves pending mode change requests.
    /// - For Parent/Admin: returns all pending requests for their children.
    /// - For ChildDevice: returns active pending request for this device.
    /// </summary>
    [HttpGet("mode-requests/pending")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<DeviceModeRequestResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> GetPendingModeRequests()
    {
        if (User.IsInRole("ChildDevice"))
        {
            var deviceIdClaim = User.FindFirstValue("DeviceId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(deviceIdClaim, out var deviceId) || deviceId <= 0)
            {
                return Forbid();
            }

            var singleResult = await _deviceService.GetPendingModeRequestForDeviceAsync(deviceId);
            if (singleResult.Data == null)
            {
                return NoContent();
            }
            return Ok(singleResult.Data);
        }

        var result = await _deviceService.GetPendingModeRequestsForParentAsync(CurrentUserId, IsAdmin);
        return Ok(result.Data);
    }

    /// <summary>
    /// Retrieves a specific mode change request by ID.
    /// </summary>
    [HttpGet("mode-requests/{id:int}")]
    [Authorize]
    [ProducesResponseType(typeof(DeviceModeRequestResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceModeRequestResponseDto>> GetModeRequestById(int id)
    {
        var isChild = User.IsInRole("ChildDevice");
        int callerId = CurrentUserId;
        int childId = 0;

        if (isChild)
        {
            var deviceIdClaim = User.FindFirstValue("DeviceId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(deviceIdClaim, out callerId);
            var childIdClaim = User.FindFirstValue("ChildId");
            int.TryParse(childIdClaim, out childId);
        }

        var result = await _deviceService.GetModeRequestByIdAsync(id, callerId, isChild, childId, IsAdmin);
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
    /// Approves a child device mode change request.
    /// Restricted strictly to authenticated Parent/Admin owning the child.
    /// </summary>
    [HttpPost("mode-requests/{id:int}/approve")]
    [Authorize(Roles = "Parent,Admin")]
    [ProducesResponseType(typeof(DeviceModeRequestResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceModeRequestResponseDto>> ApproveModeRequest(int id)
    {
        var result = await _deviceService.ApproveModeRequestAsync(id, CurrentUserId, IsAdmin);
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
    /// Rejects a child device mode change request.
    /// Restricted strictly to authenticated Parent/Admin owning the child.
    /// </summary>
    [HttpPost("mode-requests/{id:int}/reject")]
    [Authorize(Roles = "Parent,Admin")]
    [ProducesResponseType(typeof(DeviceModeRequestResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceModeRequestResponseDto>> RejectModeRequest(int id)
    {
        var result = await _deviceService.RejectModeRequestAsync(id, CurrentUserId, IsAdmin);
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
    /// Executes an approved mode change or unlinking on the child device using the approval token.
    /// Restricted strictly to authenticated ChildDevice.
    /// </summary>
    [HttpPost("mode-requests/{id:int}/execute")]
    [Authorize(Roles = "ChildDevice")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExecuteModeChange(int id, [FromBody] ExecuteModeChangeDto dto)
    {
        var deviceIdClaim = User.FindFirstValue("DeviceId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(deviceIdClaim, out var deviceId) || deviceId <= 0)
        {
            return Forbid();
        }

        var result = await _deviceService.ExecuteModeChangeAsync(deviceId, id, dto);
        if (!result.IsSuccess)
        {
            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(new { message = result.ErrorMessage }),
                _ => BadRequest(new { message = result.ErrorMessage })
            };
        }

        return Ok(new { message = "Chuyển chế độ thành công." });
    }

    /// <summary>
    /// Direct unlock: parent physically enters their credentials on the child device to switch mode or unlink immediately.
    /// Restricted to authenticated ChildDevice token.
    /// </summary>
    [HttpPost("mode-requests/direct-unlock")]
    [Authorize(Roles = "ChildDevice")]
    [ProducesResponseType(typeof(DeviceModeRequestResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DeviceModeRequestResponseDto>> DirectParentUnlock([FromBody] DirectParentUnlockDto dto)
    {
        var deviceIdClaim = User.FindFirstValue("DeviceId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        var childIdClaim = User.FindFirstValue("ChildId");

        if (!int.TryParse(deviceIdClaim, out var deviceId) || deviceId <= 0 ||
            !int.TryParse(childIdClaim, out var childId) || childId <= 0)
        {
            return Forbid();
        }

        var result = await _deviceService.DirectParentUnlockAsync(deviceId, childId, dto);
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
