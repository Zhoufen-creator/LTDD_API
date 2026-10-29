namespace tran_nguyen_vi_khang_api.DTOs.Device;

public class DeviceResponseDto
{
    public int Id { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string? DeviceIdentifier { get; set; }
    public string? Platform { get; set; }
    public int ChildId { get; set; }
    public bool IsActive { get; set; }
    public int? BatteryPercentage { get; set; }
    public bool? IsCharging { get; set; }
    public DateTime? BatteryUpdatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
