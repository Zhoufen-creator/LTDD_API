using Microsoft.EntityFrameworkCore;
using tran_nguyen_vi_khang_api.Data;
using tran_nguyen_vi_khang_api.DTOs.Activity;
using tran_nguyen_vi_khang_api.DTOs.Common;
using tran_nguyen_vi_khang_api.Models;
using tran_nguyen_vi_khang_api.Services.Interfaces;

namespace tran_nguyen_vi_khang_api.Services;

public class ActivityService : IActivityService
{
    private readonly AppDbContext _context;

    public ActivityService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Processes a batch of activity events from an enrolled child device with idempotent deduplication.
    /// </summary>
    public async Task<ServiceResult<BatchActivitySyncResponseDto>> SyncBatchActivitiesAsync(int deviceId, int childId, BatchActivitySyncDto dto)
    {
        if (dto.Events == null || dto.Events.Count == 0)
        {
            return ServiceResult<BatchActivitySyncResponseDto>.Success(new BatchActivitySyncResponseDto
            {
                ReceivedCount = 0,
                SyncedCount = 0,
                SkippedDuplicatesCount = 0,
                SyncedClientEventIds = new List<string>()
            });
        }

        // Verify device enrollment and child binding
        var deviceValid = await _context.Devices
            .AsNoTracking()
            .AnyAsync(d => d.Id == deviceId && d.ChildId == childId && d.IsActive);

        if (!deviceValid)
        {
            return ServiceResult<BatchActivitySyncResponseDto>.Forbidden("Thiết bị không hợp lệ hoặc đã bị vô hiệu hóa.");
        }

        // Extract client event IDs for batch deduplication
        var clientEventIds = dto.Events
            .Where(e => !string.IsNullOrWhiteSpace(e.ClientEventId))
            .Select(e => e.ClientEventId.Trim())
            .Distinct()
            .ToList();

        // Query existing IDs already in the database
        var existingIdsList = await _context.DeviceActivityLogs
            .AsNoTracking()
            .Where(a => clientEventIds.Contains(a.ClientEventId))
            .Select(a => a.ClientEventId)
            .ToListAsync();
        var existingEventIds = existingIdsList.ToHashSet();

        var newLogs = new List<DeviceActivityLog>();
        var seenInBatch = new HashSet<string>(existingEventIds);

        foreach (var ev in dto.Events)
        {
            var eventId = ev.ClientEventId?.Trim();
            if (string.IsNullOrWhiteSpace(eventId))
            {
                continue;
            }

            // Deduplicate if already present in DB or already added earlier in this batch
            if (seenInBatch.Contains(eventId))
            {
                continue;
            }

            seenInBatch.Add(eventId);

            // Compute or sanitize duration
            var duration = ev.DurationSeconds;
            if (duration == null && ev.EndTime.HasValue && ev.EndTime.Value >= ev.StartTime)
            {
                duration = (int)(ev.EndTime.Value - ev.StartTime).TotalSeconds;
            }

            newLogs.Add(new DeviceActivityLog
            {
                ClientEventId = eventId,
                DeviceId = deviceId,
                ChildId = childId,
                EventType = ev.EventType?.Trim() ?? "General",
                PackageName = ev.PackageName?.Trim(),
                AppName = ev.AppName?.Trim(),
                StartTime = ev.StartTime,
                EndTime = ev.EndTime,
                DurationSeconds = duration,
                RuleId = ev.RuleId,
                ViolationAction = ev.ViolationAction?.Trim(),
                ViolationDetails = ev.ViolationDetails?.Trim(),
                RecordedAt = ev.RecordedAt,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (newLogs.Count > 0)
        {
            _context.DeviceActivityLogs.AddRange(newLogs);
            await _context.SaveChangesAsync();
        }

        var response = new BatchActivitySyncResponseDto
        {
            ReceivedCount = dto.Events.Count,
            SyncedCount = newLogs.Count,
            SkippedDuplicatesCount = dto.Events.Count - newLogs.Count,
            SyncedClientEventIds = newLogs.Select(l => l.ClientEventId).ToList()
        };

        return ServiceResult<BatchActivitySyncResponseDto>.Success(response);
    }

    /// <summary>
    /// Retrieves paginated activity history and usage summary for a child with parent ownership verification.
    /// </summary>
    public async Task<ServiceResult<ChildActivityHistoryResponseDto>> GetChildActivityHistoryAsync(
        int childId,
        int parentId,
        bool isAdmin,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? eventType = null,
        string? packageName = null,
        int page = 1,
        int pageSize = 50)
    {
        // Enforce valid pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 50;

        // Verify child ownership
        var child = await _context.Children
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == childId);

        if (child == null)
        {
            return ServiceResult<ChildActivityHistoryResponseDto>.NotFound($"Không tìm thấy hồ sơ trẻ em ID {childId}.");
        }

        if (!isAdmin && child.ParentId != parentId)
        {
            return ServiceResult<ChildActivityHistoryResponseDto>.Forbidden("Bạn không có quyền xem lịch sử hoạt động của trẻ em này.");
        }

        // Base query for activity logs
        var query = _context.DeviceActivityLogs
            .Include(a => a.Device)
            .Include(a => a.Rule)
            .AsNoTracking()
            .Where(a => a.ChildId == childId);

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.RecordedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.RecordedAt <= toDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var trimmedType = eventType.Trim();
            query = query.Where(a => a.EventType == trimmedType);
        }

        if (!string.IsNullOrWhiteSpace(packageName))
        {
            var trimmedPkg = packageName.Trim();
            query = query.Where(a => a.PackageName == trimmedPkg);
        }

        var totalEvents = await query.CountAsync();

        var events = await query
            .OrderByDescending(a => a.RecordedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ActivityLogResponseDto
            {
                Id = a.Id,
                ClientEventId = a.ClientEventId,
                DeviceId = a.DeviceId,
                DeviceName = a.Device != null ? a.Device.DeviceName : string.Empty,
                ChildId = a.ChildId,
                EventType = a.EventType,
                PackageName = a.PackageName,
                AppName = a.AppName,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                DurationSeconds = a.DurationSeconds,
                RuleId = a.RuleId,
                RuleName = a.Rule != null ? a.Rule.RuleName : null,
                ViolationAction = a.ViolationAction,
                ViolationDetails = a.ViolationDetails,
                RecordedAt = a.RecordedAt,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();

        // Calculate today's summary metrics (UTC)
        var todayStart = DateTime.UtcNow.Date;
        var todayLogs = await _context.DeviceActivityLogs
            .AsNoTracking()
            .Where(a => a.ChildId == childId && a.RecordedAt >= todayStart)
            .ToListAsync();

        var totalScreenTime = todayLogs
            .Where(a => a.EventType == "AppSession" && a.DurationSeconds.HasValue)
            .Sum(a => a.DurationSeconds!.Value);

        var ruleViolations = todayLogs.Count(a => a.EventType == "RuleViolation");
        var screenUnlocks = todayLogs.Count(a => a.EventType == "DeviceUnlocked");

        var topApps = todayLogs
            .Where(a => a.EventType == "AppSession" && !string.IsNullOrWhiteSpace(a.PackageName))
            .GroupBy(a => new { PackageName = a.PackageName!, AppName = a.AppName ?? a.PackageName ?? string.Empty })
            .Select(g => new AppUsageSummaryItemDto
            {
                PackageName = g.Key.PackageName,
                AppName = g.Key.AppName,
                TotalDurationSeconds = g.Sum(x => x.DurationSeconds ?? 0),
                SessionCount = g.Count()
            })
            .OrderByDescending(x => x.TotalDurationSeconds)
            .Take(5)
            .ToList();

        var response = new ChildActivityHistoryResponseDto
        {
            ChildId = childId,
            ChildName = child.FullName,
            TotalEvents = totalEvents,
            Page = page,
            PageSize = pageSize,
            Summary = new ActivitySummaryDto
            {
                TotalScreenTimeSecondsToday = totalScreenTime,
                RuleViolationsToday = ruleViolations,
                ScreenUnlockCountToday = screenUnlocks,
                TopAppsToday = topApps
            },
            Events = events
        };

        return ServiceResult<ChildActivityHistoryResponseDto>.Success(response);
    }
}
