using System.ComponentModel.DataAnnotations;

namespace tran_nguyen_vi_khang_api.DTOs.Device;

public class CreatePairingTicketDto
{
    [Required(ErrorMessage = "ChildId is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "ChildId must be a positive integer.")]
    public int ChildId { get; set; }

    /// <summary>
    /// Optional ticket validity duration in seconds (default: 600 seconds / 10 minutes).
    /// </summary>
    [Range(1, 86400, ErrorMessage = "ValiditySeconds must be between 1 and 86400 seconds.")]
    public int? ValiditySeconds { get; set; }
}
