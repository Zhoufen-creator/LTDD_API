namespace tran_nguyen_vi_khang_api.Models;

public class MonitoringRule
{
    public int Id { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>
    /// JSON configuration specific to the RuleType:
    /// - AppBlock: {"targetPackage":"com.example.app","appName":"Example App"}
    /// - TimeLimit: {"targetPackage":"com.example.app","appName":"Example App","dailyLimitMinutes":60}
    /// - BedTime: {"bedtimeStart":"22:00","bedtimeEnd":"06:00","daysOfWeek":[1,2,3,4,5,6,7],"timezone":"Asia/Ho_Chi_Minh"}
    /// </summary>
    public string? RuleConfig { get; set; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Incremental version number, incremented on every edit.
    /// Used by child devices to verify rule freshness.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Status on client devices: ServerSaved, PendingSync, SyncedToDevice, Active, PermissionDenied, Disabled.
    /// </summary>
    public string SyncStatus { get; set; } = "ServerSaved";

    public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;

    public int ChildId { get; set; }
    public Child Child { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
