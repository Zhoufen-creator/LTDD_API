namespace tran_nguyen_vi_khang_api.Models;

public class Device
{
    public int Id { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string? DeviceIdentifier { get; set; }
    public string? Platform { get; set; }
    public int ChildId { get; set; }
    public Child Child { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public int? BatteryPercentage { get; set; }
    public bool? IsCharging { get; set; }
    public DateTime? BatteryUpdatedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
