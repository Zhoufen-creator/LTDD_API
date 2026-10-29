using System.ComponentModel.DataAnnotations;

namespace tran_nguyen_vi_khang_api.DTOs.Child;

/// <summary>
/// Data transfer object for creating a new child profile.
/// </summary>
public class CreateChildDto
{
    [Required(ErrorMessage = "FullName is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "FullName must be between 1 and 100 characters.")]
    public string FullName { get; set; } = string.Empty;

    public DateTime? DateOfBirth { get; set; }

    /// <summary>
    /// Foreign key for the parent user.
    /// 
    /// SECURITY INTEGRATION NOTICE:
    /// In the current phase without authentication/JWT, this field is accepted from the client.
    /// In production with JWT authentication, ParentId MUST NOT be accepted from the request body.
    /// Instead, it must be extracted from the authenticated ClaimsPrincipal 
    /// (e.g., User.FindFirstValue(ClaimTypes.NameIdentifier)) to prevent malicious users from
    /// creating children under another parent's account.
    /// </summary>
    [Required(ErrorMessage = "ParentId is required.")]
    public int ParentId { get; set; }
}

