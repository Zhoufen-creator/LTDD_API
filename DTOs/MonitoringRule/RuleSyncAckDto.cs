using System.ComponentModel.DataAnnotations;

namespace tran_nguyen_vi_khang_api.DTOs.MonitoringRule;

public class RuleSyncAckDto
{
    [Required]
    public int RuleId { get; set; }

    public int Version { get; set; }

    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "SyncedToDevice"; // SyncedToDevice, Active, PermissionDenied, Failed

    [StringLength(500)]
    public string? Message { get; set; }
}

public class BatchRuleSyncAckDto
{
    [Required]
    public List<RuleSyncAckDto> Acks { get; set; } = new();
}

