using tran_nguyen_vi_khang_api.DTOs.Activity;
using tran_nguyen_vi_khang_api.DTOs.Common;

namespace tran_nguyen_vi_khang_api.Services.Interfaces;

public interface IActivityService
{
    /// <summary>
    /// Processes a batch of activity events from an enrolled child device with idempotent deduplication.
    /// </summary>
    Task<ServiceResult<BatchActivitySyncResponseDto>> SyncBatchActivitiesAsync(int deviceId, int childId, BatchActivitySyncDto dto);

    /// <summary>
    /// Retrieves paginated activity history and usage summary for a child with parent ownership verification.
    /// </summary>
    Task<ServiceResult<ChildActivityHistoryResponseDto>> GetChildActivityHistoryAsync(
        int childId,
        int parentId,
        bool isAdmin,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? eventType = null,
        string? packageName = null,
        int page = 1,
        int pageSize = 50);
}

