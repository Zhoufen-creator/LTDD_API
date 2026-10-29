namespace tran_nguyen_vi_khang_api.DTOs.SafetyAdvisor;

/// <summary>
/// Summary of child GPS location recency and coordinates.
/// </summary>
public class SafetyLocationSummaryDto
{
    public int? LocationId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? Accuracy { get; set; }
    public DateTime? RecordedAt { get; set; }
    public bool HasLocation => RecordedAt.HasValue && Latitude.HasValue && Longitude.HasValue;
    public bool IsStaleLocation { get; set; }
    public int? AgeMinutes { get; set; }
}

