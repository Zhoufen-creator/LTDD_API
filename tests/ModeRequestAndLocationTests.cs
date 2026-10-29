using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using tran_nguyen_vi_khang_api.Data;
using tran_nguyen_vi_khang_api.DTOs.Common;
using tran_nguyen_vi_khang_api.DTOs.Device;
using tran_nguyen_vi_khang_api.DTOs.Location;
using tran_nguyen_vi_khang_api.Models;
using tran_nguyen_vi_khang_api.Services;
using tran_nguyen_vi_khang_api.Services.Interfaces;
using Xunit;

namespace tran_nguyen_vi_khang_api.Tests;

public class ModeRequestAndLocationTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly DeviceService _deviceService;
    private readonly LocationService _locationService;

    public ModeRequestAndLocationTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _cache = new MemoryCache(new MemoryCacheOptions());

        var config = new ConfigurationBuilder().Build();
        var jwtService = new JwtTokenService(config);

        _deviceService = new DeviceService(_context, _cache, jwtService);
        _locationService = new LocationService(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _cache.Dispose();
    }

    private async Task<(User parent, Child child, Device device)> SeedHierarchyAsync()
    {
        var parent = new User
        {
            FullName = "Nguyễn Văn Phụ Huynh",
            Email = "parent@example.com",
            // PBKDF2 hash of "SecurePass123!"
            PasswordHash = "u8jP6/iH/w/U+1o0X40tkw==:4q4F+FwYvV4s60kX1fX0lD7a7qR1Y4q1L3Y6L5G2m5s=",
            Role = "Parent",
            IsActive = true
        };
        _context.Users.Add(parent);
        await _context.SaveChangesAsync();

        var child = new Child
        {
            FullName = "Nguyễn Bé Con",
            ParentId = parent.Id
        };
        _context.Children.Add(child);
        await _context.SaveChangesAsync();

        var device = new Device
        {
            DeviceName = "Điện thoại Trẻ em",
            Platform = "Android",
            ChildId = child.Id,
            IsActive = true
        };
        _context.Devices.Add(device);
        await _context.SaveChangesAsync();

        return (parent, child, device);
    }

    // =========================================================================
    // PART A TESTS: DEVICE MODE REQUEST & PARENT AUTHORIZATION WORKFLOW
    // =========================================================================

    [Fact]
    public async Task CreateModeRequest_CreatesPendingRequest_Successfully()
    {
        var (parent, child, device) = await SeedHierarchyAsync();

        var result = await _deviceService.CreateModeRequestAsync(device.Id, child.Id, new CreateModeRequestDto
        {
            TargetMode = "ParentMode",
            Reason = "Xin phép mở máy học bài"
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("Pending", result.Data.Status);
        Assert.Equal("ParentMode", result.Data.TargetMode);
        Assert.Equal("Xin phép mở máy học bài", result.Data.Reason);
        Assert.Null(result.Data.ApprovalToken);
    }

    [Fact]
    public async Task CreateModeRequest_WhenPendingAlreadyExists_ReturnsExistingWithoutDuplicate()
    {
        var (parent, child, device) = await SeedHierarchyAsync();

        var first = await _deviceService.CreateModeRequestAsync(device.Id, child.Id, new CreateModeRequestDto
        {
            TargetMode = "ParentMode",
            Reason = "Lần 1"
        });

        var second = await _deviceService.CreateModeRequestAsync(device.Id, child.Id, new CreateModeRequestDto
        {
            TargetMode = "ParentMode",
            Reason = "Lần 2 (Spam / Retry)"
        });

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Data!.Id, second.Data!.Id);

        var totalInDb = await _context.DeviceModeRequests.CountAsync();
        Assert.Equal(1, totalInDb);
    }

    [Fact]
    public async Task Parent_CanViewOnlyTheirChildrenPendingModeRequests()
    {
        var (parent1, child1, device1) = await SeedHierarchyAsync();

        // Create second parent and child
        var parent2 = new User { FullName = "Parent 2", Email = "p2@example.com", Role = "Parent" };
        _context.Users.Add(parent2);
        await _context.SaveChangesAsync();

        var child2 = new Child { FullName = "Child 2", ParentId = parent2.Id };
        _context.Children.Add(child2);
        await _context.SaveChangesAsync();

        var device2 = new Device { DeviceName = "Device 2", ChildId = child2.Id, IsActive = true };
        _context.Devices.Add(device2);
        await _context.SaveChangesAsync();

        await _deviceService.CreateModeRequestAsync(device1.Id, child1.Id, new CreateModeRequestDto { TargetMode = "ParentMode" });
        await _deviceService.CreateModeRequestAsync(device2.Id, child2.Id, new CreateModeRequestDto { TargetMode = "Unlink" });

        // Parent 1 queries
        var parent1Requests = await _deviceService.GetPendingModeRequestsForParentAsync(parent1.Id, isAdmin: false);
        Assert.True(parent1Requests.IsSuccess);
        Assert.Single(parent1Requests.Data!);
        Assert.Equal(device1.Id, parent1Requests.Data!.First().DeviceId);
    }

    [Fact]
    public async Task Parent_ApprovesRequest_GeneratesApprovalToken()
    {
        var (parent, child, device) = await SeedHierarchyAsync();

        var created = await _deviceService.CreateModeRequestAsync(device.Id, child.Id, new CreateModeRequestDto
        {
            TargetMode = "ParentMode"
        });

        var approveResult = await _deviceService.ApproveModeRequestAsync(created.Data!.Id, parent.Id, isAdmin: false);

        Assert.True(approveResult.IsSuccess);
        Assert.Equal("Approved", approveResult.Data!.Status);
        Assert.False(string.IsNullOrWhiteSpace(approveResult.Data.ApprovalToken));
    }

    [Fact]
    public async Task Parent_RejectsRequest_MarksStatusRejected()
    {
        var (parent, child, device) = await SeedHierarchyAsync();

        var created = await _deviceService.CreateModeRequestAsync(device.Id, child.Id, new CreateModeRequestDto
        {
            TargetMode = "ParentMode"
        });

        var rejectResult = await _deviceService.RejectModeRequestAsync(created.Data!.Id, parent.Id, isAdmin: false);

        Assert.True(rejectResult.IsSuccess);
        Assert.Equal("Rejected", rejectResult.Data!.Status);
        Assert.Null(rejectResult.Data.ApprovalToken);
    }

    [Fact]
    public async Task ChildDevice_ExecutesApprovedModeChange_WithValidToken_SucceedsAndConsumesToken()
    {
        var (parent, child, device) = await SeedHierarchyAsync();

        var created = await _deviceService.CreateModeRequestAsync(device.Id, child.Id, new CreateModeRequestDto
        {
            TargetMode = "Unlink"
        });

        var approved = await _deviceService.ApproveModeRequestAsync(created.Data!.Id, parent.Id);
        var token = approved.Data!.ApprovalToken!;

        var executeResult = await _deviceService.ExecuteModeChangeAsync(device.Id, created.Data.Id, new ExecuteModeChangeDto
        {
            ApprovalToken = token
        });

        Assert.True(executeResult.IsSuccess);

        // Verification: request is Executed, token cleared, device is deactivated
        var recordInDb = await _context.DeviceModeRequests.FindAsync(created.Data.Id);
        Assert.Equal("Executed", recordInDb!.Status);
        Assert.Null(recordInDb.ApprovalToken);

        var deviceInDb = await _context.Devices.FindAsync(device.Id);
        Assert.False(deviceInDb!.IsActive);
    }

    [Fact]
    public async Task ChildDevice_TriesToExecute_WithWrongOrUnapprovedToken_Fails()
    {
        var (parent, child, device) = await SeedHierarchyAsync();

        var created = await _deviceService.CreateModeRequestAsync(device.Id, child.Id, new CreateModeRequestDto
        {
            TargetMode = "ParentMode"
        });

        // Request is still Pending, not Approved
        var executeResult = await _deviceService.ExecuteModeChangeAsync(device.Id, created.Data!.Id, new ExecuteModeChangeDto
        {
            ApprovalToken = "fake_invalid_token"
        });

        Assert.False(executeResult.IsSuccess);
    }

    // =========================================================================
    // PART B TESTS: LOCATION SYNC, HAVERSINE & DISPLACEMENT THRESHOLD
    // =========================================================================

    [Fact]
    public void CalculateHaversineDistance_ReturnsAccurateMeters()
    {
        // Landmark 81 (Ho Chi Minh City): 10.7950, 106.7218
        // Ben Thanh Market: 10.7725, 106.6980
        // Expected distance: ~3.6 km (3608 meters)
        var distance = LocationService.CalculateHaversineDistance(10.7950, 106.7218, 10.7725, 106.6980);

        Assert.InRange(distance, 3500.0, 3700.0);
    }

    [Fact]
    public async Task RecordLocation_FirstFix_RecordsAsPeriodicWithZeroDisplacement()
    {
        var (parent, child, device) = await SeedHierarchyAsync();

        var result = await _locationService.RecordLocationAsync(device.Id, child.Id, new CreateLocationDto
        {
            Latitude = 10.762622,
            Longitude = 106.660172,
            Accuracy = 15.0
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("Periodic", result.Data!.EventType);
        Assert.Equal(0.0, result.Data.DisplacementMeters);
    }

    [Fact]
    public async Task RecordLocation_DisplacementUnder100m_DoesNotSpamDuplicateDbRecords()
    {
        var (parent, child, device) = await SeedHierarchyAsync();

        // 1. Initial location
        await _locationService.RecordLocationAsync(device.Id, child.Id, new CreateLocationDto
        {
            Latitude = 10.762622,
            Longitude = 106.660172,
            Accuracy = 15.0
        });

        // 2. Second location: tiny jitter (~10 meters away)
        var second = await _locationService.RecordLocationAsync(device.Id, child.Id, new CreateLocationDto
        {
            Latitude = 10.762650,
            Longitude = 106.660200,
            Accuracy = 12.0
        });

        Assert.True(second.IsSuccess);
        Assert.Equal("Periodic", second.Data!.EventType);

        // Database should only have 1 consolidated record (updated timestamp) rather than 2 bloat records
        var totalRecords = await _context.DeviceLocations.CountAsync();
        Assert.Equal(1, totalRecords);
    }

    [Fact]
    public async Task RecordLocation_DisplacementOver100m_RecordsSignificantMovement()
    {
        var (parent, child, device) = await SeedHierarchyAsync();

        // 1. Initial location
        await _locationService.RecordLocationAsync(device.Id, child.Id, new CreateLocationDto
        {
            Latitude = 10.762622,
            Longitude = 106.660172,
            Accuracy = 15.0
        });

        // 2. Moved ~500 meters away
        var second = await _locationService.RecordLocationAsync(device.Id, child.Id, new CreateLocationDto
        {
            Latitude = 10.767000,
            Longitude = 106.660172,
            Accuracy = 10.0
        });

        Assert.True(second.IsSuccess);
        Assert.Equal("SignificantMovement", second.Data!.EventType);
        Assert.NotNull(second.Data.DisplacementMeters);
        Assert.True(second.Data.DisplacementMeters > 100.0);

        // Now there should be 2 records (Initial + Movement Event)
        var totalRecords = await _context.DeviceLocations.CountAsync();
        Assert.Equal(2, totalRecords);
    }

    [Fact]
    public async Task LocationHistory_PreventsCrossChildAccess()
    {
        var (parent, child, device) = await SeedHierarchyAsync();

        // Another unauthorized parent
        var otherParent = new User { FullName = "Other Parent", Email = "other@example.com", Role = "Parent" };
        _context.Users.Add(otherParent);
        await _context.SaveChangesAsync();

        // Unauthorized query
        var result = await _locationService.GetLocationHistoryAsync(child.Id, otherParent.Id, isAdmin: false);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    }

    // =========================================================================
    // PART C TESTS: EMAIL SENDER FAILURE HANDLING
    // =========================================================================

    [Fact]
    public async Task EmailSenderService_ReturnsFalse_WhenSmtpIsNotConfigured()
    {
        // Empty configuration (no SMTP host/user configured)
        var emptyConfig = new ConfigurationBuilder().Build();
        var emailSender = new EmailSenderService(emptyConfig, NullLogger<EmailSenderService>.Instance);

        var result = await emailSender.SendOtpEmailAsync("user@example.com", "123456");

        // Must return false genuinely instead of simulated success
        Assert.False(result);
    }
}
