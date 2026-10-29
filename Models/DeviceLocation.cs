namespace tran_nguyen_vi_khang_api.Models;

public class DeviceLocation
{
    public int Id { get; set; }

    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public int ChildId { get; set; }
    public Child Child { get; set; } = null!;

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Accuracy { get; set; }

    /// <summary>
    /// Event type: Periodic, SignificantMovement, ManualRefresh
    /// </summary>
    public string EventType { get; set; } = "Periodic";

    /// <summary>
    /// Distance in meters calculated from the previous reference location.
    /// </summary>
    public double? DisplacementMeters { get; set; }

    public DateTime RecordedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

