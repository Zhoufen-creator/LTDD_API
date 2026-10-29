namespace tran_nguyen_vi_khang_api.DTOs.Device;

public class PairingTicketResponseDto
{
    public string TicketId { get; set; } = string.Empty;
    public string PairingCode { get; set; } = string.Empty;
    public string QrPayload { get; set; } = string.Empty;
    public int ChildId { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

