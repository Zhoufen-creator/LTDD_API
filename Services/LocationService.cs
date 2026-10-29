using Microsoft.EntityFrameworkCore;
using tran_nguyen_vi_khang_api.Data;
using tran_nguyen_vi_khang_api.DTOs.Common;
using tran_nguyen_vi_khang_api.DTOs.Location;
using tran_nguyen_vi_khang_api.Models;
using tran_nguyen_vi_khang_api.Services.Interfaces;

namespace tran_nguyen_vi_khang_api.Services;

public class LocationService : ILocationService
{
    private readonly AppDbContext _context;

    public LocationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceResult<DeviceLocationResponseDto>> RecordLocationAsync(int deviceId, int childId, CreateLocationDto dto)
    {
        var device = await _context.Devices
            .Include(d => d.Child)
            .FirstOrDefaultAsync(d => d.Id == deviceId);

        if (device == null)
        {
            return ServiceResult<DeviceLocationResponseDto>.NotFound($"Device with ID {deviceId} was not found.");
        }

        if (!device.IsActive)
        {
            return ServiceResult<DeviceLocationResponseDto>.BadRequest("Device is inactive or has been revoked.");
        }

        if (device.ChildId != childId)
        {
            return ServiceResult<DeviceLocationResponseDto>.BadRequest($"Device {deviceId} does not belong to child {childId}.");
        }

        if (dto.Latitude < -90.0 || dto.Latitude > 90.0)
        {
            return ServiceResult<DeviceLocationResponseDto>.BadRequest("Latitude must be between -90 and 90 degrees.");
        }

        if (dto.Longitude < -180.0 || dto.Longitude > 180.0)
        {
            return ServiceResult<DeviceLocationResponseDto>.BadRequest("Longitude must be between -180 and 180 degrees.");
        }

        if (dto.Accuracy.HasValue && dto.Accuracy.Value < 0)
        {
            return ServiceResult<DeviceLocationResponseDto>.BadRequest("Accuracy cannot be negative.");
        }

        var nowUtc = DateTime.UtcNow;
        var recordedAtUtc = dto.RecordedAt.HasValue
            ? (dto.RecordedAt.Value.Kind == DateTimeKind.Utc
                ? dto.RecordedAt.Value
                : dto.RecordedAt.Value.ToUniversalTime())
            : nowUtc;

        if (recordedAtUtc > nowUtc.AddMinutes(10))
        {
            return ServiceResult<DeviceLocationResponseDto>.BadRequest("RecordedAt timestamp cannot be in the future.");
        }

        if (recordedAtUtc < nowUtc.AddDays(-30))
        {
            return ServiceResult<DeviceLocationResponseDto>.BadRequest("RecordedAt timestamp is too old (exceeds 30 days).");
        }

        // If battery data was sent along with location, synchronize to device record
        if (dto.BatteryPercentage.HasValue)
        {
            device.BatteryPercentage = dto.BatteryPercentage.Value;
            device.IsCharging = dto.IsCharging ?? false;
            device.BatteryUpdatedAt = nowUtc;
        }

        // Find previous latest location to determine displacement and filter noise
        var previousLocation = await _context.DeviceLocations
            .Where(l => l.ChildId == childId)
            .OrderByDescending(l => l.RecordedAt)
            .ThenByDescending(l => l.Id)
            .FirstOrDefaultAsync();

        const double MovementThresholdMeters = 100.0;
        double? displacement = null;
        string resolvedEventType;
        DeviceLocation targetLocation;

        if (previousLocation != null)
        {
            var rawDistance = CalculateHaversineDistance(
                previousLocation.Latitude,
                previousLocation.Longitude,
                dto.Latitude,
                dto.Longitude
            );
            displacement = Math.Round(rawDistance, 1);
        }

        var clientEventType = dto.EventType?.Trim();
        if (clientEventType == "ManualRefresh")
        {
            resolvedEventType = "ManualRefresh";
            targetLocation = new DeviceLocation
            {
                DeviceId = deviceId,
                ChildId = childId,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                Accuracy = dto.Accuracy,
                EventType = resolvedEventType,
                DisplacementMeters = displacement,
                RecordedAt = recordedAtUtc,
                CreatedAt = nowUtc
            };
            _context.DeviceLocations.Add(targetLocation);
        }
        else if (previousLocation == null)
        {
            resolvedEventType = "Periodic";
            targetLocation = new DeviceLocation
            {
                DeviceId = deviceId,
                ChildId = childId,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                Accuracy = dto.Accuracy,
                EventType = resolvedEventType,
                DisplacementMeters = 0.0,
                RecordedAt = recordedAtUtc,
                CreatedAt = nowUtc
            };
            _context.DeviceLocations.Add(targetLocation);
        }
        else if (displacement.HasValue && displacement.Value >= MovementThresholdMeters)
        {
            // Significant movement event detected
            resolvedEventType = "SignificantMovement";
            targetLocation = new DeviceLocation
            {
                DeviceId = deviceId,
                ChildId = childId,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                Accuracy = dto.Accuracy,
                EventType = resolvedEventType,
                DisplacementMeters = displacement.Value,
                RecordedAt = recordedAtUtc,
                CreatedAt = nowUtc
            };
            _context.DeviceLocations.Add(targetLocation);
        }
        else
        {
            // Insignificant displacement (< 100m, stationary or GPS jitter)
            resolvedEventType = "Periodic";
            if (previousLocation.EventType == "Periodic" && (nowUtc - previousLocation.CreatedAt).TotalHours < 2)
            {
                // Update timestamp & accuracy on the existing latest periodic record without database bloat
                previousLocation.RecordedAt = recordedAtUtc;
                previousLocation.Accuracy = dto.Accuracy ?? previousLocation.Accuracy;
                previousLocation.Latitude = dto.Latitude;
                previousLocation.Longitude = dto.Longitude;
                targetLocation = previousLocation;
            }
            else
            {
                // Add a milestone periodic record
                targetLocation = new DeviceLocation
                {
                    DeviceId = deviceId,
                    ChildId = childId,
                    Latitude = dto.Latitude,
                    Longitude = dto.Longitude,
                    Accuracy = dto.Accuracy,
                    EventType = resolvedEventType,
                    DisplacementMeters = displacement ?? 0.0,
                    RecordedAt = recordedAtUtc,
                    CreatedAt = nowUtc
                };
                _context.DeviceLocations.Add(targetLocation);
            }
        }

        await _context.SaveChangesAsync();

        return ServiceResult<DeviceLocationResponseDto>.Success(new DeviceLocationResponseDto
        {
            Id = targetLocation.Id,
            DeviceId = targetLocation.DeviceId,
            DeviceName = device.DeviceName,
            ChildId = targetLocation.ChildId,
            ChildName = device.Child?.FullName ?? string.Empty,
            Latitude = targetLocation.Latitude,
            Longitude = targetLocation.Longitude,
            Accuracy = targetLocation.Accuracy,
            EventType = targetLocation.EventType ?? resolvedEventType,
            DisplacementMeters = targetLocation.DisplacementMeters,
            RecordedAt = targetLocation.RecordedAt,
            CreatedAt = targetLocation.CreatedAt
        });
    }

    /// <summary>
    /// Calculates the great-circle distance between two GPS coordinates in meters using the Haversine formula.
    /// </summary>
    public static double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000.0; // Earth mean radius in meters
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);
        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return R * c;
    }

    public async Task<ServiceResult<DeviceLocationResponseDto?>> GetLatestLocationAsync(int childId, int parentId, bool isAdmin)
    {
        var child = await _context.Children
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == childId);

        if (child == null)
        {
            return ServiceResult<DeviceLocationResponseDto?>.NotFound($"Child with ID {childId} was not found.");
        }

        if (!isAdmin && child.ParentId != parentId)
        {
            return ServiceResult<DeviceLocationResponseDto?>.NotFound($"Child with ID {childId} was not found.");
        }

        var latest = await _context.DeviceLocations
            .AsNoTracking()
            .Include(l => l.Device)
            .Include(l => l.Child)
            .Where(l => l.ChildId == childId)
            .OrderByDescending(l => l.RecordedAt)
            .ThenByDescending(l => l.Id)
            .FirstOrDefaultAsync();

        if (latest == null)
        {
            return ServiceResult<DeviceLocationResponseDto?>.Success(null);
        }

        return ServiceResult<DeviceLocationResponseDto?>.Success(new DeviceLocationResponseDto
        {
            Id = latest.Id,
            DeviceId = latest.DeviceId,
            DeviceName = latest.Device?.DeviceName ?? string.Empty,
            ChildId = latest.ChildId,
            ChildName = latest.Child?.FullName ?? child.FullName,
            Latitude = latest.Latitude,
            Longitude = latest.Longitude,
            Accuracy = latest.Accuracy,
            EventType = latest.EventType ?? "Periodic",
            DisplacementMeters = latest.DisplacementMeters,
            RecordedAt = latest.RecordedAt,
            CreatedAt = latest.CreatedAt
        });
    }

    public async Task<ServiceResult<IEnumerable<DeviceLocationResponseDto>>> GetLocationHistoryAsync(int childId, int parentId, bool isAdmin, int limit = 50)
    {
        var child = await _context.Children
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == childId);

        if (child == null)
        {
            return ServiceResult<IEnumerable<DeviceLocationResponseDto>>.NotFound($"Child with ID {childId} was not found.");
        }

        if (!isAdmin && child.ParentId != parentId)
        {
            return ServiceResult<IEnumerable<DeviceLocationResponseDto>>.NotFound($"Child with ID {childId} was not found.");
        }

        if (limit <= 0) limit = 10;
        if (limit > 200) limit = 200;

        var list = await _context.DeviceLocations
            .AsNoTracking()
            .Include(l => l.Device)
            .Include(l => l.Child)
            .Where(l => l.ChildId == childId)
            .OrderByDescending(l => l.RecordedAt)
            .ThenByDescending(l => l.Id)
            .Take(limit)
            .Select(l => new DeviceLocationResponseDto
            {
                Id = l.Id,
                DeviceId = l.DeviceId,
                DeviceName = l.Device != null ? l.Device.DeviceName : string.Empty,
                ChildId = l.ChildId,
                ChildName = l.Child != null ? l.Child.FullName : child.FullName,
                Latitude = l.Latitude,
                Longitude = l.Longitude,
                Accuracy = l.Accuracy,
                EventType = l.EventType ?? "Periodic",
                DisplacementMeters = l.DisplacementMeters,
                RecordedAt = l.RecordedAt,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync();

        return ServiceResult<IEnumerable<DeviceLocationResponseDto>>.Success(list);
    }

    public async Task<ServiceResult<LocationRefreshRequestResponseDto>> CreateRefreshRequestAsync(int parentId, bool isAdmin, CreateLocationRefreshRequestDto dto)
    {
        var childQuery = _context.Children.Where(c => c.Id == dto.ChildId);
        if (!isAdmin)
        {
            childQuery = childQuery.Where(c => c.ParentId == parentId);
        }

        var child = await childQuery.FirstOrDefaultAsync();
        if (child == null)
        {
            return ServiceResult<LocationRefreshRequestResponseDto>.NotFound($"Child with ID {dto.ChildId} was not found.");
        }

        Device? targetDevice = null;
        if (dto.DeviceId.HasValue)
        {
            targetDevice = await _context.Devices
                .FirstOrDefaultAsync(d => d.Id == dto.DeviceId.Value && d.ChildId == child.Id);

            if (targetDevice == null)
            {
                return ServiceResult<LocationRefreshRequestResponseDto>.BadRequest($"Device with ID {dto.DeviceId.Value} was not found for this child.");
            }

            if (!targetDevice.IsActive)
            {
                return ServiceResult<LocationRefreshRequestResponseDto>.BadRequest("Target device is inactive or revoked.");
            }
        }
        else
        {
            targetDevice = await _context.Devices
                .Where(d => d.ChildId == child.Id && d.IsActive)
                .OrderByDescending(d => d.CreatedAt)
                .FirstOrDefaultAsync();

            if (targetDevice == null)
            {
                return ServiceResult<LocationRefreshRequestResponseDto>.BadRequest("Không tìm thấy thiết bị nào đang hoạt động liên kết với trẻ.");
            }
        }

        var nowUtc = DateTime.UtcNow;

        // Check if there is already an active Pending or Processing request within last 60 seconds (prevent duplicate spam requests)
        var recentRequest = await _context.LocationRefreshRequests
            .Include(r => r.Device)
            .Include(r => r.Child)
            .FirstOrDefaultAsync(r => r.DeviceId == targetDevice.Id &&
                                      (r.Status == "Pending" || r.Status == "Processing") &&
                                      r.ExpiresAt > nowUtc);

        if (recentRequest != null)
        {
            return ServiceResult<LocationRefreshRequestResponseDto>.Success(new LocationRefreshRequestResponseDto
            {
                Id = recentRequest.Id,
                ChildId = child.Id,
                ChildName = child.FullName,
                DeviceId = targetDevice.Id,
                DeviceName = targetDevice.DeviceName,
                Status = recentRequest.Status,
                RequestedAt = recentRequest.RequestedAt,
                ExpiresAt = recentRequest.ExpiresAt,
                CompletedAt = recentRequest.CompletedAt,
                FailureReason = recentRequest.FailureReason
            });
        }

        var refreshRequest = new LocationRefreshRequest
        {
            ChildId = child.Id,
            DeviceId = targetDevice.Id,
            Status = "Pending",
            RequestedAt = nowUtc,
            ExpiresAt = nowUtc.AddMinutes(3)
        };

        _context.LocationRefreshRequests.Add(refreshRequest);
        await _context.SaveChangesAsync();

        return ServiceResult<LocationRefreshRequestResponseDto>.Success(new LocationRefreshRequestResponseDto
        {
            Id = refreshRequest.Id,
            ChildId = child.Id,
            ChildName = child.FullName,
            DeviceId = targetDevice.Id,
            DeviceName = targetDevice.DeviceName,
            Status = refreshRequest.Status,
            RequestedAt = refreshRequest.RequestedAt,
            ExpiresAt = refreshRequest.ExpiresAt,
            CompletedAt = null,
            FailureReason = null
        });
    }

    public async Task<ServiceResult<LocationRefreshRequestResponseDto>> GetRefreshRequestStatusAsync(int parentId, bool isAdmin, int requestId)
    {
        var request = await _context.LocationRefreshRequests
            .Include(r => r.Child)
            .Include(r => r.Device)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null)
        {
            return ServiceResult<LocationRefreshRequestResponseDto>.NotFound($"Refresh request {requestId} was not found.");
        }

        if (!isAdmin && request.Child.ParentId != parentId)
        {
            return ServiceResult<LocationRefreshRequestResponseDto>.NotFound($"Refresh request {requestId} was not found.");
        }

        // Auto-expire request if past expiration
        var nowUtc = DateTime.UtcNow;
        if (request.Status == "Pending" && request.ExpiresAt <= nowUtc)
        {
            request.Status = "Expired";
            await _context.SaveChangesAsync();
        }

        return ServiceResult<LocationRefreshRequestResponseDto>.Success(new LocationRefreshRequestResponseDto
        {
            Id = request.Id,
            ChildId = request.ChildId,
            ChildName = request.Child.FullName,
            DeviceId = request.DeviceId,
            DeviceName = request.Device.DeviceName,
            Status = request.Status,
            RequestedAt = request.RequestedAt,
            ExpiresAt = request.ExpiresAt,
            CompletedAt = request.CompletedAt,
            FailureReason = request.FailureReason
        });
    }

    public async Task<ServiceResult<LocationRefreshRequestResponseDto?>> GetPendingRefreshRequestForDeviceAsync(int deviceId)
    {
        var nowUtc = DateTime.UtcNow;

        var request = await _context.LocationRefreshRequests
            .Include(r => r.Child)
            .Include(r => r.Device)
            .Where(r => r.DeviceId == deviceId && r.Status == "Pending" && r.ExpiresAt > nowUtc)
            .OrderBy(r => r.RequestedAt)
            .FirstOrDefaultAsync();

        if (request == null)
        {
            return ServiceResult<LocationRefreshRequestResponseDto?>.Success(null);
        }

        request.Status = "Processing";
        await _context.SaveChangesAsync();

        return ServiceResult<LocationRefreshRequestResponseDto?>.Success(new LocationRefreshRequestResponseDto
        {
            Id = request.Id,
            ChildId = request.ChildId,
            ChildName = request.Child.FullName,
            DeviceId = request.DeviceId,
            DeviceName = request.Device.DeviceName,
            Status = request.Status,
            RequestedAt = request.RequestedAt,
            ExpiresAt = request.ExpiresAt,
            CompletedAt = request.CompletedAt,
            FailureReason = request.FailureReason
        });
    }

    public async Task<ServiceResult<LocationRefreshRequestResponseDto>> CompleteRefreshRequestAsync(int deviceId, int requestId, CompleteRefreshRequestDto dto)
    {
        var request = await _context.LocationRefreshRequests
            .Include(r => r.Child)
            .Include(r => r.Device)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null)
        {
            return ServiceResult<LocationRefreshRequestResponseDto>.NotFound($"Refresh request {requestId} was not found.");
        }

        if (request.DeviceId != deviceId)
        {
            return ServiceResult<LocationRefreshRequestResponseDto>.BadRequest("Request does not belong to this device.");
        }

        var nowUtc = DateTime.UtcNow;

        if (dto.IsSuccess)
        {
            if (dto.Latitude.HasValue && dto.Longitude.HasValue)
            {
                var recordedAt = dto.RecordedAt.HasValue
                    ? (dto.RecordedAt.Value.Kind == DateTimeKind.Utc ? dto.RecordedAt.Value : dto.RecordedAt.Value.ToUniversalTime())
                    : nowUtc;

                var previous = await _context.DeviceLocations
                    .Where(l => l.ChildId == request.ChildId)
                    .OrderByDescending(l => l.RecordedAt)
                    .ThenByDescending(l => l.Id)
                    .FirstOrDefaultAsync();

                double? displacement = null;
                if (previous != null)
                {
                    displacement = Math.Round(CalculateHaversineDistance(
                        previous.Latitude,
                        previous.Longitude,
                        dto.Latitude.Value,
                        dto.Longitude.Value
                    ), 1);
                }

                var location = new DeviceLocation
                {
                    DeviceId = deviceId,
                    ChildId = request.ChildId,
                    Latitude = dto.Latitude.Value,
                    Longitude = dto.Longitude.Value,
                    Accuracy = dto.Accuracy,
                    EventType = "ManualRefresh",
                    DisplacementMeters = displacement,
                    RecordedAt = recordedAt,
                    CreatedAt = nowUtc
                };
                _context.DeviceLocations.Add(location);
            }

            if (dto.BatteryPercentage.HasValue && request.Device != null)
            {
                request.Device.BatteryPercentage = dto.BatteryPercentage.Value;
                request.Device.IsCharging = dto.IsCharging ?? false;
                request.Device.BatteryUpdatedAt = nowUtc;
            }

            request.Status = "Completed";
            request.CompletedAt = nowUtc;
            request.FailureReason = null;
        }
        else
        {
            request.Status = "Failed";
            request.CompletedAt = nowUtc;
            request.FailureReason = dto.FailureReason ?? "Thiết bị không thể xác định vị trí GPS.";
        }

        await _context.SaveChangesAsync();

        return ServiceResult<LocationRefreshRequestResponseDto>.Success(new LocationRefreshRequestResponseDto
        {
            Id = request.Id,
            ChildId = request.ChildId,
            ChildName = request.Child.FullName,
            DeviceId = request.DeviceId,
            DeviceName = request.Device?.DeviceName ?? string.Empty,
            Status = request.Status,
            RequestedAt = request.RequestedAt,
            ExpiresAt = request.ExpiresAt,
            CompletedAt = request.CompletedAt,
            FailureReason = request.FailureReason
        });
    }
}
