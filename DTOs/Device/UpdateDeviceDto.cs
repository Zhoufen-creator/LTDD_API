using System.ComponentModel.DataAnnotations;

namespace tran_nguyen_vi_khang_api.DTOs.Device;

/// <summary>
/// Data transfer object for updating an existing device.
/// Server-controlled fields (Id, CreatedAt) cannot be changed via this request.
/// </summary>
public class UpdateDeviceDto
{
    [Required(ErrorMessage = "DeviceName is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "DeviceName must be between 1 and 100 characters.")]
    public string DeviceName { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "DeviceIdentifier cannot exceed 255 characters.")]
    public string? DeviceIdentifier { get; set; }

    [StringLength(50, ErrorMessage = "Platform cannot exceed 50 characters.")]
    public string? Platform { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional: Can reassign the device to another child.
    /// In production with authentication, the target child must also belong to the authenticated parent.
    /// </summary>
    public int? ChildId { get; set; }
}

