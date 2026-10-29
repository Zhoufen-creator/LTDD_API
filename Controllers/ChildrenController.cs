using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using tran_nguyen_vi_khang_api.DTOs.Child;
using tran_nguyen_vi_khang_api.DTOs.Common;
using tran_nguyen_vi_khang_api.DTOs.SafetyAdvisor;
using tran_nguyen_vi_khang_api.Services.Interfaces;

namespace tran_nguyen_vi_khang_api.Controllers;

/// <summary>
/// API endpoints for managing Child profiles.
/// All endpoints strictly enforce JWT Authentication and Parent Data Ownership.
/// </summary>
[Authorize(Roles = "Parent,Admin")]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChildrenController : ControllerBase
{
    private readonly IChildService _childService;
    private readonly ISafetyAdvisorService _safetyAdvisorService;

    public ChildrenController(IChildService childService, ISafetyAdvisorService safetyAdvisorService)
    {
        _childService = childService;
        _safetyAdvisorService = safetyAdvisorService;
    }

    private int CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private bool IsAdmin => User.IsInRole("Admin");

    /// <summary>
    /// Retrieves all child profiles belonging to the authenticated parent.
    /// Admins can optionally filter by parentId.
    /// </summary>
    /// <param name="parentId">Optional parent ID filter (Admin only).</param>
    /// <response code="200">List of child profiles successfully retrieved.</response>
    /// <response code="401">Unauthorized: Missing or invalid JWT bearer token.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ChildResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<ChildResponseDto>>> GetAll([FromQuery] int? parentId = null)
    {
        var children = await _childService.GetAllAsync(CurrentUserId, IsAdmin, parentId);
        return Ok(children);
    }

    /// <summary>
    /// Retrieves a specific child profile by ID with strict ownership verification.
    /// </summary>
    /// <param name="id">Child ID.</param>
    /// <response code="200">Child profile successfully retrieved.</response>
    /// <response code="401">Unauthorized: Missing or invalid JWT bearer token.</response>
    /// <response code="404">Child with specified ID was not found or does not belong to you.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ChildResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChildResponseDto>> GetById(int id)
    {
        var child = await _childService.GetByIdAsync(id, CurrentUserId, IsAdmin);
        if (child == null)
        {
            return NotFound(new { message = $"Child with ID {id} was not found." });
        }

        return Ok(child);
    }

    /// <summary>
    /// Analyzes stored battery, device, and location telemetry to return actionable Smart Safety Advisor recommendations.
    /// Strictly restricted to authenticated Parent owning the child or Admin.
    /// </summary>
    /// <param name="id">Target child ID.</param>
    /// <response code="200">Safety assessment and actionable recommendations.</response>
    /// <response code="401">Unauthorized: Missing or invalid JWT bearer token.</response>
    /// <response code="404">Child with specified ID was not found or does not belong to you.</response>
    [HttpGet("{id:int}/safety-advisor")]
    [ProducesResponseType(typeof(SafetyAdvisorResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SafetyAdvisorResponseDto>> GetSafetyAdvisor(int id)
    {
        var result = await _safetyAdvisorService.GetSafetyAdvisorAsync(id, CurrentUserId, IsAdmin);

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
    /// Creates a new child profile under the authenticated parent account.
    /// Server overrides and assigns ownership to the authenticated user.
    /// </summary>
    /// <param name="dto">Child creation payload.</param>
    /// <response code="201">Child successfully created.</response>
    /// <response code="400">Invalid input data.</response>
    /// <response code="401">Unauthorized: Missing or invalid JWT bearer token.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ChildResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ChildResponseDto>> Create([FromBody] CreateChildDto dto)
    {
        var result = await _childService.CreateAsync(dto, CurrentUserId);

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
    /// Updates an existing child profile with ownership verification.
    /// </summary>
    /// <param name="id">Child ID to update.</param>
    /// <param name="dto">Updated child details.</param>
    /// <response code="200">Child profile successfully updated.</response>
    /// <response code="400">Invalid input data.</response>
    /// <response code="401">Unauthorized: Missing or invalid JWT bearer token.</response>
    /// <response code="404">Child with specified ID was not found or does not belong to you.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ChildResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChildResponseDto>> Update(int id, [FromBody] UpdateChildDto dto)
    {
        var result = await _childService.UpdateAsync(id, dto, CurrentUserId, IsAdmin);

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
    /// Deletes a child profile and cascades deletion to enrolled devices and rules with ownership verification.
    /// </summary>
    /// <param name="id">Child ID to delete.</param>
    /// <response code="204">Child profile successfully deleted.</response>
    /// <response code="401">Unauthorized: Missing or invalid JWT bearer token.</response>
    /// <response code="404">Child with specified ID was not found or does not belong to you.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _childService.DeleteAsync(id, CurrentUserId, IsAdmin);

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
}
