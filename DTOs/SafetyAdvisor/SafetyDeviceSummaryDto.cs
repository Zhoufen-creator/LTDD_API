namespace tran_nguyen_vi_khang_api.DTOs.SafetyAdvisor;

/// <summary>
/// Summary of child device battery and connectivity state.
/// </summary>
public class SafetyDeviceSummaryDto
{
    public int DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int? BatteryPercentage { get; set; }
    public bool? IsCharging { get; set; }
    public DateTime? BatteryUpdatedAt { get; set; }
    public bool HasBatteryData => BatteryPercentage.HasValue;
    public bool IsLowBattery => BatteryPercentage.HasValue && BatteryPercentage.Value < 20;
    public bool IsStaleBattery { get; set; }
}

