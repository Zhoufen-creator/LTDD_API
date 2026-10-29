using System.ComponentModel.DataAnnotations;

namespace tran_nguyen_vi_khang_api.DTOs.Location;

public class CompleteRefreshRequestDto
{
    public bool IsSuccess { get; set; } = true;

    [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90 degrees.")]
    public double? Latitude { get; set; }

    [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180 degrees.")]
    public double? Longitude { get; set; }

    [Range(0.0, double.MaxValue, ErrorMessage = "Accuracy must be non-negative.")]
    public double? Accuracy { get; set; }

    public DateTime? RecordedAt { get; set; }

    public string? FailureReason { get; set; }

    [Range(0, 100, ErrorMessage = "Battery percentage must be between 0 and 100.")]
    public int? BatteryPercentage { get; set; }

    public bool? IsCharging { get; set; }
}

