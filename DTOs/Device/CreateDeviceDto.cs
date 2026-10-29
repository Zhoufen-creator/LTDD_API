using System.ComponentModel.DataAnnotations;

namespace tran_nguyen_vi_khang_api.DTOs.Device;

/// <summary>
/// Data transfer object for enrolling or creating a new device for a child.
/// </summary>
public class CreateDeviceDto
{
    [Required(ErrorMessage = "DeviceName is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "DeviceName must be between 1 and 100 characters.")]
    public string DeviceName { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "DeviceIdentifier cannot exceed 255 characters.")]
    public string? DeviceIdentifier { get; set; }

    [StringLength(50, ErrorMessage = "Platform cannot exceed 50 characters.")]
    public string? Platform { get; set; }

    /// <summary>
    /// Child ID to whom this device is assigned.
    /// In production with authentication, the service must verify that the child belongs to the authenticated parent.
    /// </summary>
    [Required(ErrorMessage = "ChildId is required.")]
    public int ChildId { get; set; }

    public bool IsActive { get; set; } = true;
}

