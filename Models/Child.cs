namespace tran_nguyen_vi_khang_api.Models;

public class Child
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public int ParentId { get; set; }
    public User Parent { get; set; } = null!;

    public ICollection<Device> Devices { get; set; } = new List<Device>();
    public ICollection<MonitoringRule> MonitoringRules { get; set; }
        = new List<MonitoringRule>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
