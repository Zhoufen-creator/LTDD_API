using System.ComponentModel.DataAnnotations;

namespace tran_nguyen_vi_khang_api.DTOs.Child;

/// <summary>
/// Data transfer object for updating an existing child profile.
/// Server-controlled fields (Id, CreatedAt) and ParentId ownership cannot be changed via this request.
/// </summary>
public class UpdateChildDto
{
    [Required(ErrorMessage = "FullName is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "FullName must be between 1 and 100 characters.")]
    public string FullName { get; set; } = string.Empty;

    public DateTime? DateOfBirth { get; set; }
}

