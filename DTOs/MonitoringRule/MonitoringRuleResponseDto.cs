namespace tran_nguyen_vi_khang_api.DTOs.MonitoringRule;

public class MonitoringRuleResponseDto
{
    public int Id { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public string? RuleConfig { get; set; }
    public int Version { get; set; } = 1;
    public string SyncStatus { get; set; } = "ServerSaved";
    public DateTime LastModifiedAt { get; set; }
    public int ChildId { get; set; }
    public DateTime CreatedAt { get; set; }
}

