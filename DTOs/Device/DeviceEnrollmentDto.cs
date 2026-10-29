using System.ComponentModel.DataAnnotations;

namespace tran_nguyen_vi_khang_api.DTOs.Device;

public class DeviceEnrollmentDto
{
    [Required(ErrorMessage = "PairingCode is required.")]
    public string PairingCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "DeviceName is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "DeviceName must be between 1 and 100 characters.")]
    public string DeviceName { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "DeviceIdentifier cannot exceed 200 characters.")]
    public string? DeviceIdentifier { get; set; }

    [StringLength(50, ErrorMessage = "Platform cannot exceed 50 characters.")]
    public string? Platform { get; set; }
}

