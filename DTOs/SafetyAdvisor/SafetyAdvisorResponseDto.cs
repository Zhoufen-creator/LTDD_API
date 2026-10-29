namespace tran_nguyen_vi_khang_api.DTOs.SafetyAdvisor;

/// <summary>
/// Root response object for Smart Safety Advisor evaluation.
/// </summary>
public class SafetyAdvisorResponseDto
{
    public int ChildId { get; set; }
    public string ChildName { get; set; } = string.Empty;

    /// <summary>
    /// Evaluated overall safety status:
    /// - Safe: All checks normal based on available data.
    /// - NeedsAttention: Minor or informational issues (e.g. stale location, low battery while charging).
    /// - Warning: High priority issues (e.g. low battery not charging).
    /// - InsufficientData: No device or no telemetry data available to make an evaluation.
    /// </summary>
    public string OverallStatus { get; set; } = "InsufficientData";

    /// <summary>
    /// Short status title in Vietnamese.
    /// </summary>
    public string StatusTitle { get; set; } = string.Empty;

    /// <summary>
    /// Comprehensive summary message in Vietnamese.
    /// </summary>
    public string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when advisor evaluated the child's status.
    /// </summary>
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Indicates whether enough telemetry data was present to make a reliable assessment.
    /// When false, OverallStatus is guaranteed to be InsufficientData (never Safe).
    /// </summary>
    public bool HasSufficientData { get; set; }

    public int TotalDevices { get; set; }
    public int ActiveDevices { get; set; }

    public SafetyDeviceSummaryDto? PrimaryDevice { get; set; }
    public List<SafetyDeviceSummaryDto> Devices { get; set; } = new();
    public SafetyLocationSummaryDto? LatestLocation { get; set; }

    /// <summary>
    /// Actionable warnings generated during evaluation.
    /// </summary>
    public List<SafetyWarningDto> Warnings { get; set; } = new();
}

