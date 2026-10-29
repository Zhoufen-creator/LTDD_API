using System.ComponentModel.DataAnnotations;

namespace tran_nguyen_vi_khang_api.DTOs.Device;

public class CreateModeRequestDto
{
    [Required(ErrorMessage = "TargetMode is required.")]
    [RegularExpression("^(ParentMode|Unlink)$", ErrorMessage = "TargetMode must be 'ParentMode' or 'Unlink'.")]
    public string TargetMode { get; set; } = "ParentMode";

    [MaxLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
    public string? Reason { get; set; }
}

public class DeviceModeRequestResponseDto
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public int ChildId { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public string TargetMode { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ApprovalToken { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ExecutedAt { get; set; }
}

public class ExecuteModeChangeDto
{
    [Required(ErrorMessage = "ApprovalToken is required.")]
    public string ApprovalToken { get; set; } = string.Empty;
}

public class DirectParentUnlockDto
{
    [Required(ErrorMessage = "Parent email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parent password is required.")]
    public string Password { get; set; } = string.Empty;

    [RegularExpression("^(ParentMode|Unlink)$", ErrorMessage = "TargetMode must be 'ParentMode' or 'Unlink'.")]
    public string TargetMode { get; set; } = "ParentMode";
}

