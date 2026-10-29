namespace tran_nguyen_vi_khang_api.Models;

public class LocationRefreshRequest
{
    public int Id { get; set; }

    public int ChildId { get; set; }
    public Child Child { get; set; } = null!;

    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    /// <summary>
    /// Pending, Processing, Completed, Failed, Expired
    /// </summary>
    public string Status { get; set; } = "Pending";

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(3);
    public DateTime? CompletedAt { get; set; }
    public string? FailureReason { get; set; }
}

