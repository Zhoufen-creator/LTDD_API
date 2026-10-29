namespace tran_nguyen_vi_khang_api.DTOs.Device;

public class EnrollmentResponseDto
{
    public string DeviceToken { get; set; } = string.Empty;
    public int DeviceId { get; set; }
    public int ChildId { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

