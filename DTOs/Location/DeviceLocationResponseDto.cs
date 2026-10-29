namespace tran_nguyen_vi_khang_api.DTOs.Location;

public class DeviceLocationResponseDto
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public int ChildId { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Accuracy { get; set; }
    public string EventType { get; set; } = "Periodic";
    public double? DisplacementMeters { get; set; }
    public DateTime RecordedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

