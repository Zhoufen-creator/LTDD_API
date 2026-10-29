using Microsoft.EntityFrameworkCore;
using tran_nguyen_vi_khang_api.Data;
using tran_nguyen_vi_khang_api.DTOs.Activity;
using tran_nguyen_vi_khang_api.DTOs.Common;
using tran_nguyen_vi_khang_api.DTOs.MonitoringRule;
using tran_nguyen_vi_khang_api.Models;
using tran_nguyen_vi_khang_api.Services;
using Xunit;

namespace tran_nguyen_vi_khang_api.Tests;

public class ActivityAndRuleEngineTests
{
    private AppDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task SyncBatchActivitiesAsync_DeduplicatesExistingAndInBatchDuplicateIds()
    {
        var db = CreateInMemoryDbContext(nameof(SyncBatchActivitiesAsync_DeduplicatesExistingAndInBatchDuplicateIds));
        var activityService = new ActivityService(db);

        // Seed Child and Device
        var child = new Child { Id = 1, FullName = "Bé Bo", ParentId = 10 };
        var device = new Device { Id = 100, DeviceName = "ASUS ROG Phone", ChildId = 1, IsActive = true };
        db.Children.Add(child);
        db.Devices.Add(device);

        // Existing log in DB with GUID-1
        db.DeviceActivityLogs.Add(new DeviceActivityLog
        {
            ClientEventId = "GUID-0001",
            DeviceId = 100,
            ChildId = 1,
            EventType = "AppSession",
            PackageName = "com.google.android.youtube",
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            RecordedAt = DateTime.UtcNow.AddMinutes(-30)
        });
        await db.SaveChangesAsync();

        // Batch containing:
        // 1. GUID-0001 (already in DB -> should skip)
        // 2. GUID-0002 (new -> should sync)
        // 3. GUID-0002 (duplicate in batch -> should skip)
        var batchDto = new BatchActivitySyncDto
        {
            Events = new List<CreateActivityLogDto>
            {
                new()
                {
                    ClientEventId = "GUID-0001",
                    EventType = "AppSession",
                    PackageName = "com.google.android.youtube",
                    StartTime = DateTime.UtcNow.AddMinutes(-10),
                    RecordedAt = DateTime.UtcNow.AddMinutes(-10)
                },
                new()
                {
                    ClientEventId = "GUID-0002",
                    EventType = "AppSession",
                    PackageName = "com.zhiliaoapp.musically",
                    AppName = "TikTok",
                    StartTime = DateTime.UtcNow.AddMinutes(-5),
                    EndTime = DateTime.UtcNow,
                    DurationSeconds = 300,
                    RecordedAt = DateTime.UtcNow
                },
                new()
                {
                    ClientEventId = "GUID-0002",
                    EventType = "AppSession",
                    PackageName = "com.zhiliaoapp.musically",
                    StartTime = DateTime.UtcNow.AddMinutes(-5),
                    RecordedAt = DateTime.UtcNow
                }
            }
        };

        var result = await activityService.SyncBatchActivitiesAsync(deviceId: 100, childId: 1, batchDto);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data!.ReceivedCount);
        Assert.Equal(1, result.Data.SyncedCount);
        Assert.Equal(2, result.Data.SkippedDuplicatesCount);
        Assert.Single(result.Data.SyncedClientEventIds);
        Assert.Equal("GUID-0002", result.Data.SyncedClientEventIds[0]);

        var totalLogs = await db.DeviceActivityLogs.CountAsync();
        Assert.Equal(2, totalLogs); // GUID-0001 + GUID-0002
    }

    [Fact]
    public async Task SyncBatchActivitiesAsync_ReturnsForbidden_WhenDeviceInactiveOrMismatch()
    {
        var db = CreateInMemoryDbContext(nameof(SyncBatchActivitiesAsync_ReturnsForbidden_WhenDeviceInactiveOrMismatch));
        var activityService = new ActivityService(db);

        var child = new Child { Id = 2, FullName = "Bé Bi", ParentId = 10 };
        var inactiveDevice = new Device { Id = 200, DeviceName = "Old Phone", ChildId = 2, IsActive = false };
        db.Children.Add(child);
        db.Devices.Add(inactiveDevice);
        await db.SaveChangesAsync();

        var batchDto = new BatchActivitySyncDto
        {
            Events = new List<CreateActivityLogDto>
            {
                new()
                {
                    ClientEventId = "GUID-9999",
                    EventType = "ScreenOn",
                    StartTime = DateTime.UtcNow,
                    RecordedAt = DateTime.UtcNow
                }
            }
        };

        var result = await activityService.SyncBatchActivitiesAsync(deviceId: 200, childId: 2, batchDto);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.Forbidden, result.ErrorType);
    }

    [Fact]
    public async Task GetChildActivityHistoryAsync_CalculatesDailySummaryAndAppUsage()
    {
        var db = CreateInMemoryDbContext(nameof(GetChildActivityHistoryAsync_CalculatesDailySummaryAndAppUsage));
        var activityService = new ActivityService(db);

        var child = new Child { Id = 3, FullName = "Bé An", ParentId = 55 };
        var device = new Device { Id = 300, DeviceName = "Samsung Tab", ChildId = 3, IsActive = true };
        db.Children.Add(child);
        db.Devices.Add(device);

        var today = DateTime.UtcNow.Date.AddHours(10);

        // Seed: 1200 seconds of YouTube, 600 seconds of TikTok, 1 RuleViolation, 2 ScreenUnlocks
        db.DeviceActivityLogs.AddRange(
            new DeviceActivityLog
            {
                ClientEventId = "EV-1",
                DeviceId = 300,
                ChildId = 3,
                EventType = "AppSession",
                PackageName = "com.google.android.youtube",
                AppName = "YouTube",
                StartTime = today.AddMinutes(-30),
                EndTime = today.AddMinutes(-10),
                DurationSeconds = 1200,
                RecordedAt = today.AddMinutes(-10)
            },
            new DeviceActivityLog
            {
                ClientEventId = "EV-2",
                DeviceId = 300,
                ChildId = 3,
                EventType = "AppSession",
                PackageName = "com.zhiliaoapp.musically",
                AppName = "TikTok",
                StartTime = today.AddMinutes(-10),
                EndTime = today,
                DurationSeconds = 600,
                RecordedAt = today
            },
            new DeviceActivityLog
            {
                ClientEventId = "EV-3",
                DeviceId = 300,
                ChildId = 3,
                EventType = "RuleViolation",
                PackageName = "com.zhiliaoapp.musically",
                ViolationAction = "Blocked",
                ViolationDetails = "Quy tắc khóa ứng dụng kích hoạt",
                StartTime = today,
                RecordedAt = today
            },
            new DeviceActivityLog
            {
                ClientEventId = "EV-4",
                DeviceId = 300,
                ChildId = 3,
                EventType = "DeviceUnlocked",
                StartTime = today.AddMinutes(-35),
                RecordedAt = today.AddMinutes(-35)
            },
            new DeviceActivityLog
            {
                ClientEventId = "EV-5",
                DeviceId = 300,
                ChildId = 3,
                EventType = "DeviceUnlocked",
                StartTime = today.AddMinutes(-5),
                RecordedAt = today.AddMinutes(-5)
            }
        );
        await db.SaveChangesAsync();

        var result = await activityService.GetChildActivityHistoryAsync(
            childId: 3,
            parentId: 55,
            isAdmin: false,
            page: 1,
            pageSize: 10);

        Assert.True(result.IsSuccess);
        var response = result.Data!;
        Assert.Equal(5, response.TotalEvents);
        Assert.Equal(1800, response.Summary.TotalScreenTimeSecondsToday); // 1200 + 600
        Assert.Equal(1, response.Summary.RuleViolationsToday);
        Assert.Equal(2, response.Summary.ScreenUnlockCountToday);

        // Top apps: YouTube first (1200s), TikTok second (600s)
        Assert.Equal(2, response.Summary.TopAppsToday.Count);
        Assert.Equal("com.google.android.youtube", response.Summary.TopAppsToday[0].PackageName);
        Assert.Equal(1200, response.Summary.TopAppsToday[0].TotalDurationSeconds);
        Assert.Equal("com.zhiliaoapp.musically", response.Summary.TopAppsToday[1].PackageName);
        Assert.Equal(600, response.Summary.TopAppsToday[1].TotalDurationSeconds);
    }

    [Fact]
    public async Task GetChildActivityHistoryAsync_EnforcesParentOwnership()
    {
        var db = CreateInMemoryDbContext(nameof(GetChildActivityHistoryAsync_EnforcesParentOwnership));
        var activityService = new ActivityService(db);

        var child = new Child { Id = 4, FullName = "Bé Lan", ParentId = 77 };
        db.Children.Add(child);
        await db.SaveChangesAsync();

        // Calling with different parent ID 88 (not Admin)
        var result = await activityService.GetChildActivityHistoryAsync(
            childId: 4,
            parentId: 88,
            isAdmin: false);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.Forbidden, result.ErrorType);

        // Admin override allowed
        var adminResult = await activityService.GetChildActivityHistoryAsync(
            childId: 4,
            parentId: 88,
            isAdmin: true);

        Assert.True(adminResult.IsSuccess);
    }

    [Fact]
    public async Task DeviceService_AcknowledgeRuleSyncAsync_UpdatesSyncStatus()
    {
        var db = CreateInMemoryDbContext(nameof(DeviceService_AcknowledgeRuleSyncAsync_UpdatesSyncStatus));
        var deviceService = new DeviceService(db, null!, null!);

        var child = new Child { Id = 5, FullName = "Bé Minh", ParentId = 99 };
        var rule1 = new MonitoringRule
        {
            Id = 501,
            RuleName = "Khóa TikTok",
            RuleType = "AppBlock",
            ChildId = 5,
            SyncStatus = "ServerSaved",
            Version = 1
        };
        var rule2 = new MonitoringRule
        {
            Id = 502,
            RuleName = "Giới hạn YouTube",
            RuleType = "TimeLimit",
            ChildId = 5,
            SyncStatus = "ServerSaved",
            Version = 2
        };
        db.Children.Add(child);
        db.MonitoringRules.AddRange(rule1, rule2);
        await db.SaveChangesAsync();

        var ackDto = new BatchRuleSyncAckDto
        {
            Acks = new List<RuleSyncAckDto>
            {
                new() { RuleId = 501, Version = 1, Status = "Active" },
                new() { RuleId = 502, Version = 2, Status = "SyncedToDevice" }
            }
        };

        var ackResult = await deviceService.AcknowledgeRuleSyncAsync(childId: 5, ackDto);

        Assert.True(ackResult.IsSuccess);

        var updatedRule1 = await db.MonitoringRules.FindAsync(501);
        var updatedRule2 = await db.MonitoringRules.FindAsync(502);

        Assert.Equal("Active", updatedRule1!.SyncStatus);
        Assert.Equal("SyncedToDevice", updatedRule2!.SyncStatus);
    }
}

