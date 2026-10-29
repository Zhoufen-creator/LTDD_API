using Microsoft.EntityFrameworkCore;
using tran_nguyen_vi_khang_api.Data;
using tran_nguyen_vi_khang_api.DTOs.Common;
using tran_nguyen_vi_khang_api.Models;
using tran_nguyen_vi_khang_api.Services;
using Xunit;

namespace tran_nguyen_vi_khang_api.Tests;

public class SafetyAdvisorServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly SafetyAdvisorService _advisorService;
    private readonly DateTime _fixedEvaluationTime = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    public SafetyAdvisorServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _advisorService = new SafetyAdvisorService(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<(User parent, Child child, Device device)> SeedParentChildWithDeviceAsync(
        int? batteryPercentage = 80,
        bool? isCharging = false,
        DateTime? batteryUpdatedAt = null,
        DateTime? locationRecordedAt = null)
    {
        var parent = new User
        {
            FullName = "Nguyen Parent",
            Email = $"parent_{Guid.NewGuid()}@example.com",
            Role = "Parent",
            IsActive = true
        };
        _context.Users.Add(parent);
        await _context.SaveChangesAsync();

        var child = new Child
        {
            FullName = "Nguyen Child",
            ParentId = parent.Id,
            CreatedAt = _fixedEvaluationTime.AddDays(-10)
        };
        _context.Children.Add(child);
        await _context.SaveChangesAsync();

        var device = new Device
        {
            DeviceName = "Child Galaxy Phone",
            DeviceIdentifier = "test-device-uuid-1",
            Platform = "Android",
            ChildId = child.Id,
            IsActive = true,
            BatteryPercentage = batteryPercentage,
            IsCharging = isCharging,
            BatteryUpdatedAt = batteryUpdatedAt ?? _fixedEvaluationTime.AddMinutes(-5),
            CreatedAt = _fixedEvaluationTime.AddDays(-5)
        };
        _context.Devices.Add(device);
        await _context.SaveChangesAsync();

        if (locationRecordedAt.HasValue)
        {
            var location = new DeviceLocation
            {
                DeviceId = device.Id,
                ChildId = child.Id,
                Latitude = 10.762622,
                Longitude = 106.660172,
                Accuracy = 10.0,
                RecordedAt = locationRecordedAt.Value,
                CreatedAt = locationRecordedAt.Value
            };
            _context.DeviceLocations.Add(location);
            await _context.SaveChangesAsync();
        }

        return (parent, child, device);
    }

    // =========================================================================
    // TEST 1: Battery below 20% (Low Battery Warning)
    // =========================================================================
    [Fact]
    public async Task GetSafetyAdvisor_WhenBatteryBelow20NotCharging_GeneratesLowBatteryWarning()
    {
        // Arrange: 15% battery, not charging, fresh location
        var (parent, child, _) = await SeedParentChildWithDeviceAsync(
            batteryPercentage: 15,
            isCharging: false,
            batteryUpdatedAt: _fixedEvaluationTime.AddMinutes(-5),
            locationRecordedAt: _fixedEvaluationTime.AddMinutes(-10)
        );

        // Act
        var result = await _advisorService.GetSafetyAdvisorAsync(
            child.Id, parent.Id, isAdmin: false, evaluationTimeUtc: _fixedEvaluationTime);

        // Assert
        Assert.True(result.IsSuccess);
        var data = result.Data!;
        Assert.Equal("Warning", data.OverallStatus);
        Assert.True(data.HasSufficientData);

        var lowBatteryWarning = data.Warnings.FirstOrDefault(w => w.Type == "LowBattery");
        Assert.NotNull(lowBatteryWarning);
        Assert.Equal("Warning", lowBatteryWarning.Severity);
        Assert.Equal("15%", lowBatteryWarning.Reading);
        Assert.Equal("ContactChild", lowBatteryWarning.ActionType);
        Assert.Contains("15%", lowBatteryWarning.Message);
    }

    [Fact]
    public async Task GetSafetyAdvisor_WhenBatteryBelow20AndCharging_GeneratesNeedsAttentionWarning()
    {
        // Arrange: 18% battery, charging, fresh location
        var (parent, child, _) = await SeedParentChildWithDeviceAsync(
            batteryPercentage: 18,
            isCharging: true,
            batteryUpdatedAt: _fixedEvaluationTime.AddMinutes(-2),
            locationRecordedAt: _fixedEvaluationTime.AddMinutes(-5)
        );

        // Act
        var result = await _advisorService.GetSafetyAdvisorAsync(
            child.Id, parent.Id, isAdmin: false, evaluationTimeUtc: _fixedEvaluationTime);

        // Assert
        Assert.True(result.IsSuccess);
        var data = result.Data!;
        Assert.Equal("NeedsAttention", data.OverallStatus);

        var lowBatteryWarning = data.Warnings.FirstOrDefault(w => w.Type == "LowBattery");
        Assert.NotNull(lowBatteryWarning);
        Assert.Equal("NeedsAttention", lowBatteryWarning.Severity);
        Assert.Equal("ChargeDevice", lowBatteryWarning.ActionType);
        Assert.Contains("đang được cắm sạc", lowBatteryWarning.Message);
    }

    // =========================================================================
    // TEST 2: Battery at or above 20%
    // =========================================================================
    [Fact]
    public async Task GetSafetyAdvisor_WhenBatteryAtOrAbove20_NoBatteryWarning()
    {
        // Arrange: exactly 20% battery, fresh location
        var (parent, child, _) = await SeedParentChildWithDeviceAsync(
            batteryPercentage: 20,
            isCharging: false,
            batteryUpdatedAt: _fixedEvaluationTime.AddMinutes(-5),
            locationRecordedAt: _fixedEvaluationTime.AddMinutes(-5)
        );

        // Act
        var result = await _advisorService.GetSafetyAdvisorAsync(
            child.Id, parent.Id, isAdmin: false, evaluationTimeUtc: _fixedEvaluationTime);

        // Assert
        Assert.True(result.IsSuccess);
        var data = result.Data!;
        Assert.Equal("Safe", data.OverallStatus);
        Assert.Empty(data.Warnings);
        Assert.NotNull(data.PrimaryDevice);
        Assert.Equal(20, data.PrimaryDevice.BatteryPercentage);
    }

    // =========================================================================
    // TEST 3: Missing battery information
    // =========================================================================
    [Fact]
    public async Task GetSafetyAdvisor_WhenBatteryDataIsMissing_GeneratesMissingBatteryNotice()
    {
        // Arrange: Device exists but BatteryPercentage is null, fresh location
        var (parent, child, _) = await SeedParentChildWithDeviceAsync(
            batteryPercentage: null,
            isCharging: null,
            batteryUpdatedAt: null,
            locationRecordedAt: _fixedEvaluationTime.AddMinutes(-5)
        );

        // Act
        var result = await _advisorService.GetSafetyAdvisorAsync(
            child.Id, parent.Id, isAdmin: false, evaluationTimeUtc: _fixedEvaluationTime);

        // Assert
        Assert.True(result.IsSuccess);
        var data = result.Data!;
        Assert.Equal("NeedsAttention", data.OverallStatus);

        var missingBatteryWarning = data.Warnings.FirstOrDefault(w => w.Type == "MissingBattery");
        Assert.NotNull(missingBatteryWarning);
        Assert.Equal("Chưa có dữ liệu", missingBatteryWarning.Reading);
        Assert.DoesNotContain(data.Warnings, w => w.Type == "LowBattery");
    }

    // =========================================================================
    // TEST 4: Stale location older than 30 minutes
    // =========================================================================
    [Fact]
    public async Task GetSafetyAdvisor_WhenLocationOlderThan30Minutes_GeneratesStaleLocationWarning()
    {
        // Arrange: Location recorded 45 minutes ago (stale), normal battery (80%)
        var recordedAt = _fixedEvaluationTime.AddMinutes(-45);
        var (parent, child, _) = await SeedParentChildWithDeviceAsync(
            batteryPercentage: 80,
            isCharging: false,
            batteryUpdatedAt: _fixedEvaluationTime.AddMinutes(-5),
            locationRecordedAt: recordedAt
        );

        // Act
        var result = await _advisorService.GetSafetyAdvisorAsync(
            child.Id, parent.Id, isAdmin: false, evaluationTimeUtc: _fixedEvaluationTime);

        // Assert
        Assert.True(result.IsSuccess);
        var data = result.Data!;
        Assert.Equal("NeedsAttention", data.OverallStatus);

        var staleWarning = data.Warnings.FirstOrDefault(w => w.Type == "StaleLocation");
        Assert.NotNull(staleWarning);
        Assert.Equal("NeedsAttention", staleWarning.Severity);
        Assert.Equal(recordedAt, staleWarning.Timestamp);
        Assert.Equal("RefreshLocation", staleWarning.ActionType);
        Assert.Contains("45 phút trước", staleWarning.Reading);
        Assert.NotNull(data.LatestLocation);
        Assert.True(data.LatestLocation.IsStaleLocation);
        Assert.Equal(45, data.LatestLocation.AgeMinutes);
    }

    // =========================================================================
    // TEST 5: Fresh location (within 30 minutes)
    // =========================================================================
    [Fact]
    public async Task GetSafetyAdvisor_WhenLocationWithin30Minutes_NoLocationWarning()
    {
        // Arrange: Location recorded 10 minutes ago (fresh), normal battery
        var recordedAt = _fixedEvaluationTime.AddMinutes(-10);
        var (parent, child, _) = await SeedParentChildWithDeviceAsync(
            batteryPercentage: 90,
            isCharging: false,
            batteryUpdatedAt: _fixedEvaluationTime.AddMinutes(-5),
            locationRecordedAt: recordedAt
        );

        // Act
        var result = await _advisorService.GetSafetyAdvisorAsync(
            child.Id, parent.Id, isAdmin: false, evaluationTimeUtc: _fixedEvaluationTime);

        // Assert
        Assert.True(result.IsSuccess);
        var data = result.Data!;
        Assert.Equal("Safe", data.OverallStatus);
        Assert.Empty(data.Warnings);
        Assert.NotNull(data.LatestLocation);
        Assert.False(data.LatestLocation.IsStaleLocation);
        Assert.Equal(10, data.LatestLocation.AgeMinutes);
    }

    // =========================================================================
    // TEST 6: No location data
    // =========================================================================
    [Fact]
    public async Task GetSafetyAdvisor_WhenNoLocationRecordedEver_GeneratesNoLocationWarning()
    {
        // Arrange: Device with battery, but locationRecordedAt is null
        var (parent, child, _) = await SeedParentChildWithDeviceAsync(
            batteryPercentage: 75,
            isCharging: false,
            batteryUpdatedAt: _fixedEvaluationTime.AddMinutes(-5),
            locationRecordedAt: null
        );

        // Act
        var result = await _advisorService.GetSafetyAdvisorAsync(
            child.Id, parent.Id, isAdmin: false, evaluationTimeUtc: _fixedEvaluationTime);

        // Assert
        Assert.True(result.IsSuccess);
        var data = result.Data!;
        Assert.Equal("NeedsAttention", data.OverallStatus);

        var noLocWarning = data.Warnings.FirstOrDefault(w => w.Type == "NoLocation");
        Assert.NotNull(noLocWarning);
        Assert.Equal("RefreshLocation", noLocWarning.ActionType);
        Assert.Null(data.LatestLocation);
    }

    // =========================================================================
    // TEST 7: Parent attempting to inspect child owned by another parent
    // =========================================================================
    [Fact]
    public async Task GetSafetyAdvisor_WhenParentDoesNotOwnChild_ReturnsNotFound()
    {
        // Arrange: Parent 1 owns Child
        var (ownerParent, child, _) = await SeedParentChildWithDeviceAsync(
            batteryPercentage: 80,
            isCharging: false,
            locationRecordedAt: _fixedEvaluationTime.AddMinutes(-10)
        );

        // Intruder Parent 2
        var intruderParent = new User
        {
            FullName = "Intruder Parent",
            Email = "intruder@example.com",
            Role = "Parent",
            IsActive = true
        };
        _context.Users.Add(intruderParent);
        await _context.SaveChangesAsync();

        // Act: Intruder parent requests owner's child
        var result = await _advisorService.GetSafetyAdvisorAsync(
            child.Id, intruderParent.Id, isAdmin: false, evaluationTimeUtc: _fixedEvaluationTime);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
        Assert.Null(result.Data);

        // Act 2: Admin can access regardless of parent ownership
        var adminResult = await _advisorService.GetSafetyAdvisorAsync(
            child.Id, intruderParent.Id, isAdmin: true, evaluationTimeUtc: _fixedEvaluationTime);
        Assert.True(adminResult.IsSuccess);
        Assert.NotNull(adminResult.Data);
    }

    // =========================================================================
    // TEST 8: Multiple warnings existing simultaneously
    // =========================================================================
    [Fact]
    public async Task GetSafetyAdvisor_WhenLowBatteryAndStaleLocationCoexist_ReportsBothWarnings()
    {
        // Arrange: 10% battery not charging AND location 60 minutes old
        var recordedAt = _fixedEvaluationTime.AddMinutes(-60);
        var (parent, child, _) = await SeedParentChildWithDeviceAsync(
            batteryPercentage: 10,
            isCharging: false,
            batteryUpdatedAt: _fixedEvaluationTime.AddMinutes(-5),
            locationRecordedAt: recordedAt
        );

        // Act
        var result = await _advisorService.GetSafetyAdvisorAsync(
            child.Id, parent.Id, isAdmin: false, evaluationTimeUtc: _fixedEvaluationTime);

        // Assert
        Assert.True(result.IsSuccess);
        var data = result.Data!;
        // Overall severity should be "Warning" because of low battery not charging
        Assert.Equal("Warning", data.OverallStatus);
        Assert.Equal(2, data.Warnings.Count);

        var lowBatteryWarning = data.Warnings.FirstOrDefault(w => w.Type == "LowBattery");
        var staleLocWarning = data.Warnings.FirstOrDefault(w => w.Type == "StaleLocation");

        Assert.NotNull(lowBatteryWarning);
        Assert.Equal("Warning", lowBatteryWarning.Severity);
        Assert.Equal("10%", lowBatteryWarning.Reading);

        Assert.NotNull(staleLocWarning);
        Assert.Equal("NeedsAttention", staleLocWarning.Severity);
        Assert.Equal(recordedAt, staleLocWarning.Timestamp);
    }

    // =========================================================================
    // TEST 9: Insufficient data state (No device paired)
    // =========================================================================
    [Fact]
    public async Task GetSafetyAdvisor_WhenNoDeviceIsEnrolled_ReturnsInsufficientDataStatus()
    {
        // Arrange: Child with no devices at all
        var parent = new User
        {
            FullName = "New Parent",
            Email = "newparent@example.com",
            Role = "Parent",
            IsActive = true
        };
        _context.Users.Add(parent);
        await _context.SaveChangesAsync();

        var child = new Child
        {
            FullName = "New Baby",
            ParentId = parent.Id,
            CreatedAt = _fixedEvaluationTime.AddDays(-1)
        };
        _context.Children.Add(child);
        await _context.SaveChangesAsync();

        // Act
        var result = await _advisorService.GetSafetyAdvisorAsync(
            child.Id, parent.Id, isAdmin: false, evaluationTimeUtc: _fixedEvaluationTime);

        // Assert
        Assert.True(result.IsSuccess);
        var data = result.Data!;
        Assert.False(data.HasSufficientData);
        Assert.Equal("InsufficientData", data.OverallStatus);
        Assert.Equal("Chưa đủ dữ liệu", data.StatusTitle);
        Assert.Contains(data.Warnings, w => w.Type == "NoDevice");
    }
}
