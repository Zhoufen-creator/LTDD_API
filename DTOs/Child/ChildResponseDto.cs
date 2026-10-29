namespace tran_nguyen_vi_khang_api.DTOs.Child;

public class ChildResponseDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public int ParentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int DeviceCount { get; set; }
    public int MonitoringRuleCount { get; set; }
}

