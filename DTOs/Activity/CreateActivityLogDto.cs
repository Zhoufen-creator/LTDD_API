using System.ComponentModel.DataAnnotations;

namespace tran_nguyen_vi_khang_api.DTOs.Activity;

public class CreateActivityLogDto
{
    /// <summary>
    /// Client UUID v4 for idempotency and offline deduplication.
    /// </summary>
    [Required(ErrorMessage = "ClientEventId is required.")]
    [StringLength(64, MinimumLength = 10, ErrorMessage = "ClientEventId must be a valid GUID/UUID.")]
    public string ClientEventId { get; set; } = string.Empty;

    /// <summary>
    /// EventType: AppSession, ScreenOn, ScreenOff, DeviceLocked, DeviceUnlocked, RuleViolation
    /// </summary>
    [Required(ErrorMessage = "EventType is required.")]
    [StringLength(50)]
    public string EventType { get; set; } = string.Empty;

    [StringLength(255)]
    public string? PackageName { get; set; }

    [StringLength(255)]
    public string? AppName { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    [Range(0, 86400 * 7, ErrorMessage = "DurationSeconds must be non-negative.")]
    public int? DurationSeconds { get; set; }

    public int? RuleId { get; set; }

    [StringLength(50)]
    public string? ViolationAction { get; set; }

    [StringLength(500)]
    public string? ViolationDetails { get; set; }

    [Required]
    public DateTime RecordedAt { get; set; }
}

