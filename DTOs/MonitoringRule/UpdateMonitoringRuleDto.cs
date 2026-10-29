using System.ComponentModel.DataAnnotations;

namespace tran_nguyen_vi_khang_api.DTOs.MonitoringRule;

/// <summary>
/// Data transfer object for updating an existing monitoring rule.
/// Server-controlled fields (Id, CreatedAt) cannot be changed via this request.
/// </summary>
public class UpdateMonitoringRuleDto
{
    [Required(ErrorMessage = "RuleName is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "RuleName must be between 1 and 100 characters.")]
    public string RuleName { get; set; } = string.Empty;

    [Required(ErrorMessage = "RuleType is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "RuleType must be between 1 and 50 characters.")]
    public string RuleType { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    /// <summary>
    /// JSON configuration specific to the rule type (AppBlock, TimeLimit, BedTime, etc.).
    /// </summary>
    [StringLength(4000, ErrorMessage = "RuleConfig cannot exceed 4000 characters.")]
    public string? RuleConfig { get; set; }

    public bool IsEnabled { get; set; } = true;

    [StringLength(50, ErrorMessage = "SyncStatus cannot exceed 50 characters.")]
    public string? SyncStatus { get; set; }

    /// <summary>
    /// Optional: Can reassign the monitoring rule to another child.
    /// In production with authentication, the target child must also belong to the authenticated parent.
    /// </summary>
    public int? ChildId { get; set; }
}

