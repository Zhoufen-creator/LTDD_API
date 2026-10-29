using System.ComponentModel.DataAnnotations;

namespace tran_nguyen_vi_khang_api.DTOs.Activity;

public class BatchActivitySyncDto
{
    [Required]
    [MaxLength(500, ErrorMessage = "Batch size cannot exceed 500 events per request.")]
    public List<CreateActivityLogDto> Events { get; set; } = new();
}

public class BatchActivitySyncResponseDto
{
    public int ReceivedCount { get; set; }
    public int SyncedCount { get; set; }
    public int SkippedDuplicatesCount { get; set; }
    public List<string> SyncedClientEventIds { get; set; } = new();
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

