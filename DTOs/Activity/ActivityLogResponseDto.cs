namespace tran_nguyen_vi_khang_api.DTOs.Activity;

public class ActivityLogResponseDto
{
    public int Id { get; set; }
    public string ClientEventId { get; set; } = string.Empty;
    public int DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public int ChildId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? PackageName { get; set; }
    public string? AppName { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int? DurationSeconds { get; set; }
    public int? RuleId { get; set; }
    public string? RuleName { get; set; }
    public string? ViolationAction { get; set; }
    public string? ViolationDetails { get; set; }
    public DateTime RecordedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AppUsageSummaryItemDto
{
    public string PackageName { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public int TotalDurationSeconds { get; set; }
    public int SessionCount { get; set; }
}

public class ActivitySummaryDto
{
    public int TotalScreenTimeSecondsToday { get; set; }
    public int RuleViolationsToday { get; set; }
    public int ScreenUnlockCountToday { get; set; }
    public List<AppUsageSummaryItemDto> TopAppsToday { get; set; } = new();
}

public class ChildActivityHistoryResponseDto
{
    public int ChildId { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public int TotalEvents { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public ActivitySummaryDto Summary { get; set; } = new();
    public List<ActivityLogResponseDto> Events { get; set; } = new();
}

