using System.ComponentModel.DataAnnotations;

namespace tran_nguyen_vi_khang_api.DTOs.Device;

public class UpdateBatteryDto
{
    [Required]
    [Range(0, 100, ErrorMessage = "Battery percentage must be between 0 and 100.")]
    public int BatteryPercentage { get; set; }

    public bool IsCharging { get; set; } = false;
}

