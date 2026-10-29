using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using tran_nguyen_vi_khang_api.DTOs.Common;
using tran_nguyen_vi_khang_api.DTOs.MonitoringRule;
using tran_nguyen_vi_khang_api.Services.Interfaces;

namespace tran_nguyen_vi_khang_api.Controllers;

/// <summary>
/// API endpoints for managing Parental Monitoring Rules.
/// All endpoints strictly enforce JWT Authentication and Parent Data Ownership.
/// </summary>
[Authorize(Roles = "Parent,Admin")]
[ApiController]
[Route("api/[controller]")]
[Route("api/monitoring-rules")]
[Produces("application/json")]
public class MonitoringRulesController : ControllerBase
{
    private readonly IMonitoringRuleService _ruleService;

    public MonitoringRulesController(IMonitoringRuleService ruleService)
    {
        _ruleService = ruleService;
    }

    private int CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private bool IsAdmin => User.IsInRole("Admin");

    /// <summary>
    /// Retrieves all monitoring rules belonging to the authenticated parent's children.
    /// </summary>
    /// <param name="childId">Optional child ID filter.</param>
    /// <response code="200">List of monitoring rules successfully retrieved.</response>
    /// <response code="401">Unauthorized: Missing or invalid JWT bearer token.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MonitoringRuleResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<MonitoringRuleResponseDto>>> GetAll([FromQuery] int? childId = null)
    {
        var rules = await _ruleService.GetAllAsync(CurrentUserId, IsAdmin, childId);
        return Ok(rules);
    }

    /// <summary>
    /// Retrieves a specific monitoring rule by ID with ownership verification.
    /// </summary>
    /// <param name="id">Monitoring Rule ID.</param>
    /// <response code="200">Monitoring rule successfully retrieved.</response>
    /// <response code="401">Unauthorized: Missing or invalid JWT bearer token.</response>
    /// <response code="404">Rule with specified ID was not found or does not belong to you.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(MonitoringRuleResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MonitoringRuleResponseDto>> GetById(int id)
    {
        var rule = await _ruleService.GetByIdAsync(id, CurrentUserId, IsAdmin);
        if (rule == null)
        {
            return NotFound(new { message = $"Monitoring rule with ID {id} was not found." });
        }

        return Ok(rule);
    }

    /// <summary>
    /// Creates a new monitoring rule for a child belonging to the authenticated parent.
    /// </summary>
    /// <param name="dto">Rule creation payload.</param>
    /// <response code="201">Rule successfully created.</response>
    /// <response code="400">Invalid input or target child does not belong to your account.</response>
    /// <response code="401">Unauthorized: Missing or invalid JWT bearer token.</response>
    [HttpPost]
    [ProducesResponseType(typeof(MonitoringRuleResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MonitoringRuleResponseDto>> Create([FromBody] CreateMonitoringRuleDto dto)
    {
        var result = await _ruleService.CreateAsync(dto, CurrentUserId, IsAdmin);

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
    /// Updates an existing monitoring rule with ownership verification.
    /// </summary>
    /// <param name="id">Rule ID to update.</param>
    /// <param name="dto">Updated rule details.</param>
    /// <response code="200">Rule successfully updated.</response>
    /// <response code="400">Invalid input or target child does not belong to your account.</response>
    /// <response code="401">Unauthorized: Missing or invalid JWT bearer token.</response>
    /// <response code="404">Rule with specified ID was not found or does not belong to you.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(MonitoringRuleResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MonitoringRuleResponseDto>> Update(int id, [FromBody] UpdateMonitoringRuleDto dto)
    {
        var result = await _ruleService.UpdateAsync(id, dto, CurrentUserId, IsAdmin);

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
    /// Deletes a monitoring rule by ID with ownership verification.
    /// </summary>
    /// <param name="id">Rule ID to delete.</param>
    /// <response code="204">Rule successfully deleted.</response>
    /// <response code="401">Unauthorized: Missing or invalid JWT bearer token.</response>
    /// <response code="404">Rule with specified ID was not found or does not belong to you.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _ruleService.DeleteAsync(id, CurrentUserId, IsAdmin);

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
