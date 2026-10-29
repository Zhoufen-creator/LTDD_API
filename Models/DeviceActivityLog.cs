namespace tran_nguyen_vi_khang_api.Models;

/// <summary>
/// Represents an activity event recorded on a child device and synced to the backend.
/// Supports deduplication via ClientEventId to guarantee idempotent offline batch synchronization.
/// </summary>
public class DeviceActivityLog
{
    public int Id { get; set; }

    /// <summary>
    /// Cryptographically random client-side UUID v4 used for idempotency and deduplication.
    /// </summary>
    public string ClientEventId { get; set; } = string.Empty;

    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public int ChildId { get; set; }
    public Child Child { get; set; } = null!;

    /// <summary>
    /// Event type: AppSession, ScreenOn, ScreenOff, DeviceLocked, DeviceUnlocked, RuleViolation
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Android package name for app activities or rule violations (e.g. com.zhiliaoapp.musically).
    /// </summary>
    public string? PackageName { get; set; }

    /// <summary>
    /// Human-friendly application name (e.g. TikTok, YouTube).
    /// </summary>
    public string? AppName { get; set; }

    /// <summary>
    /// Start time of the event or session in UTC.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// End time of the event or session in UTC (nullable for instantaneous events).
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Duration of the event in seconds.
    /// </summary>
    public int? DurationSeconds { get; set; }

    /// <summary>
    /// Associated monitoring rule if this activity is a rule violation.
    /// </summary>
    public int? RuleId { get; set; }
    public MonitoringRule? Rule { get; set; }

    /// <summary>
    /// Action taken if violation: Blocked, Warning, TimeExceeded, BedTimeLock
    /// </summary>
    public string? ViolationAction { get; set; }

    /// <summary>
    /// Additional context or details about the violation.
    /// </summary>
    public string? ViolationDetails { get; set; }

    /// <summary>
    /// Timestamp when the event was recorded on the child device (UTC).
    /// </summary>
    public DateTime RecordedAt { get; set; }

    /// <summary>
    /// Timestamp when this record was synchronized and persisted to the server (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

