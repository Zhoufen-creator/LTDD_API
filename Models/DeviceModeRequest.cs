namespace tran_nguyen_vi_khang_api.Models;

/// <summary>
/// Represents a child device request to change mode (e.g., Switch to Parent Mode or Unlink Device),
/// requiring explicit authorization by the child's registered parent.
/// </summary>
public class DeviceModeRequest
{
    public int Id { get; set; }

    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public int ChildId { get; set; }
    public Child Child { get; set; } = null!;

    /// <summary>
    /// Target mode or action requested: "ParentMode", "Unlink"
    /// </summary>
    public string TargetMode { get; set; } = "ParentMode";

    public string? Reason { get; set; }

    /// <summary>
    /// Request status: "Pending", "Approved", "Rejected", "Expired", "Executed"
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Cryptographic single-use token generated upon parent approval.
    /// </summary>
    public string? ApprovalToken { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(15);

    public DateTime? ReviewedAt { get; set; }

    public int? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }

    public DateTime? ExecutedAt { get; set; }
}

