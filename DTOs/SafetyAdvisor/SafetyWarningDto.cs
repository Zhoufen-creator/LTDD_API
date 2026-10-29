namespace tran_nguyen_vi_khang_api.DTOs.SafetyAdvisor;

/// <summary>
/// Represents an actionable safety advisor warning or notification.
/// </summary>
public class SafetyWarningDto
{
    /// <summary>
    /// Warning category: LowBattery, StaleLocation, NoLocation, MissingBattery, StaleBattery, NoDevice
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Severity level: Warning (Critical/Action Required), NeedsAttention (Moderate), Info (Informational)
    /// </summary>
    public string Severity { get; set; } = "NeedsAttention";

    /// <summary>
    /// Human-readable title in Vietnamese.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed actionable message in Vietnamese.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Relevant reading value (e.g. "12%", "45 phút trước").
    /// </summary>
    public string? Reading { get; set; }

    /// <summary>
    /// Associated UTC timestamp of the underlying data reading.
    /// </summary>
    public DateTime? Timestamp { get; set; }

    /// <summary>
    /// Suggested remedial action for the parent.
    /// </summary>
    public string SuggestedAction { get; set; } = string.Empty;

    /// <summary>
    /// Action category: RefreshLocation, ContactChild, ChargeDevice, PairDevice, None
    /// </summary>
    public string ActionType { get; set; } = "None";

    /// <summary>
    /// Specific device ID if the warning applies to a particular device.
    /// </summary>
    public int? DeviceId { get; set; }

    /// <summary>
    /// Specific device name if applicable.
    /// </summary>
    public string? DeviceName { get; set; }
}

