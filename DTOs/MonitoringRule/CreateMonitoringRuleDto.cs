using System.ComponentModel.DataAnnotations;

namespace tran_nguyen_vi_khang_api.DTOs.MonitoringRule;

/// <summary>
/// Data transfer object for creating a new parental monitoring rule.
/// </summary>
public class CreateMonitoringRuleDto
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

    /// <summary>
    /// Child ID to whom this monitoring rule applies.
    /// In production with authentication, the service must verify that the child belongs to the authenticated parent.
    /// </summary>
    [Required(ErrorMessage = "ChildId is required.")]
    public int ChildId { get; set; }
}

