using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using tran_nguyen_vi_khang_api.Data;
using tran_nguyen_vi_khang_api.DTOs.Common;
using tran_nguyen_vi_khang_api.DTOs.Device;
using tran_nguyen_vi_khang_api.DTOs.MonitoringRule;
using tran_nguyen_vi_khang_api.Models;
using tran_nguyen_vi_khang_api.Services.Interfaces;

namespace tran_nguyen_vi_khang_api.Services;

internal class PairingTicket
{
    public string TicketId { get; set; } = string.Empty;
    public string PairingCode { get; set; } = string.Empty;
    public int ChildId { get; set; }
    public int ParentId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsConsumed { get; set; }
    public object Lock { get; } = new object();
}

public class DeviceService : IDeviceService
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IJwtTokenService _jwtTokenService;

    public DeviceService(AppDbContext context, IMemoryCache cache, IJwtTokenService jwtTokenService)
    {
        _context = context;
        _cache = cache;
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>
    /// Retrieves devices belonging to children of the authenticated parent.
    /// </summary>
    public async Task<IEnumerable<DeviceResponseDto>> GetAllAsync(int currentUserId, bool isAdmin = false, int? childId = null)
    {
        var query = _context.Devices.AsNoTracking().AsQueryable();

        if (!isAdmin)
        {
            query = query.Where(d => d.Child.ParentId == currentUserId);
        }

        if (childId.HasValue)
        {
            query = query.Where(d => d.ChildId == childId.Value);
        }

        return await query
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DeviceResponseDto
            {
                Id = d.Id,
                DeviceName = d.DeviceName,
                DeviceIdentifier = d.DeviceIdentifier,
                Platform = d.Platform,
                ChildId = d.ChildId,
                IsActive = d.IsActive,
                BatteryPercentage = d.BatteryPercentage,
                IsCharging = d.IsCharging,
                BatteryUpdatedAt = d.BatteryUpdatedAt,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves a device by its ID with ownership verification.
    /// </summary>
    public async Task<DeviceResponseDto?> GetByIdAsync(int id, int currentUserId, bool isAdmin = false)
    {
        var query = _context.Devices
            .AsNoTracking()
            .Where(d => d.Id == id);

        if (!isAdmin)
        {
            query = query.Where(d => d.Child.ParentId == currentUserId);
        }

        return await query
            .Select(d => new DeviceResponseDto
            {
                Id = d.Id,
                DeviceName = d.DeviceName,
                DeviceIdentifier = d.DeviceIdentifier,
                Platform = d.Platform,
                ChildId = d.ChildId,
                IsActive = d.IsActive,
                BatteryPercentage = d.BatteryPercentage,
                IsCharging = d.IsCharging,
                BatteryUpdatedAt = d.BatteryUpdatedAt,
                CreatedAt = d.CreatedAt
            })
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Enrolls/creates a new device for a child.
    /// Verifies that the targeted child belongs to the authenticated parent.
    /// </summary>
    public async Task<ServiceResult<DeviceResponseDto>> CreateAsync(CreateDeviceDto dto, int currentUserId, bool isAdmin = false)
    {
        var trimmedName = dto.DeviceName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return ServiceResult<DeviceResponseDto>.BadRequest("DeviceName cannot be empty or whitespace.");
        }

        // Verify Child exists and belongs to current parent
        var childQuery = _context.Children.Where(c => c.Id == dto.ChildId);
        if (!isAdmin)
        {
            childQuery = childQuery.Where(c => c.ParentId == currentUserId);
        }

        var childExists = await childQuery.AnyAsync();
        if (!childExists)
        {
            return ServiceResult<DeviceResponseDto>.BadRequest($"Child with ID {dto.ChildId} does not exist or does not belong to your account.");
        }

        var device = new Device
        {
            DeviceName = trimmedName,
            DeviceIdentifier = dto.DeviceIdentifier?.Trim(),
            Platform = dto.Platform?.Trim(),
            ChildId = dto.ChildId,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Devices.Add(device);
        await _context.SaveChangesAsync();

        var response = new DeviceResponseDto
        {
            Id = device.Id,
            DeviceName = device.DeviceName,
            DeviceIdentifier = device.DeviceIdentifier,
            Platform = device.Platform,
            ChildId = device.ChildId,
            IsActive = device.IsActive,
            BatteryPercentage = device.BatteryPercentage,
            IsCharging = device.IsCharging,
            BatteryUpdatedAt = device.BatteryUpdatedAt,
            CreatedAt = device.CreatedAt
        };

        return ServiceResult<DeviceResponseDto>.Success(response);
    }

    /// <summary>
    /// Updates an existing device with ownership verification.
    /// </summary>
    public async Task<ServiceResult<DeviceResponseDto>> UpdateAsync(int id, UpdateDeviceDto dto, int currentUserId, bool isAdmin = false)
    {
        var query = _context.Devices.Include(d => d.Child).Where(d => d.Id == id);
        if (!isAdmin)
        {
            query = query.Where(d => d.Child.ParentId == currentUserId);
        }

        var device = await query.FirstOrDefaultAsync();
        if (device == null)
        {
            return ServiceResult<DeviceResponseDto>.NotFound($"Device with ID {id} was not found.");
        }

        var trimmedName = dto.DeviceName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return ServiceResult<DeviceResponseDto>.BadRequest("DeviceName cannot be empty or whitespace.");
        }

        // If reassigning to another child, verify target child belongs to current user
        if (dto.ChildId.HasValue && dto.ChildId.Value != device.ChildId)
        {
            var targetChildQuery = _context.Children.Where(c => c.Id == dto.ChildId.Value);
            if (!isAdmin)
            {
                targetChildQuery = targetChildQuery.Where(c => c.ParentId == currentUserId);
            }

            var targetChildExists = await targetChildQuery.AnyAsync();
            if (!targetChildExists)
            {
                return ServiceResult<DeviceResponseDto>.BadRequest($"Target child with ID {dto.ChildId.Value} does not exist or does not belong to your account.");
            }
            device.ChildId = dto.ChildId.Value;
        }

        device.DeviceName = trimmedName;
        device.DeviceIdentifier = dto.DeviceIdentifier?.Trim();
        device.Platform = dto.Platform?.Trim();
        device.IsActive = dto.IsActive;

        await _context.SaveChangesAsync();

        var response = new DeviceResponseDto
        {
            Id = device.Id,
            DeviceName = device.DeviceName,
            DeviceIdentifier = device.DeviceIdentifier,
            Platform = device.Platform,
            ChildId = device.ChildId,
            IsActive = device.IsActive,
            BatteryPercentage = device.BatteryPercentage,
            IsCharging = device.IsCharging,
            BatteryUpdatedAt = device.BatteryUpdatedAt,
            CreatedAt = device.CreatedAt
        };

        return ServiceResult<DeviceResponseDto>.Success(response);
    }

    /// <summary>
    /// Deletes a device by ID with ownership verification.
    /// </summary>
    public async Task<ServiceResult<bool>> DeleteAsync(int id, int currentUserId, bool isAdmin = false)
    {
        var query = _context.Devices.Include(d => d.Child).Where(d => d.Id == id);
        if (!isAdmin)
        {
            query = query.Where(d => d.Child.ParentId == currentUserId);
        }

        var device = await query.FirstOrDefaultAsync();
        if (device == null)
        {
            return ServiceResult<bool>.NotFound($"Device with ID {id} was not found.");
        }

        _context.Devices.Remove(device);
        await _context.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    /// <summary>
    /// Generates a single-use pairing ticket for a parent to enroll a child device via QR code.
    /// </summary>
    public async Task<ServiceResult<PairingTicketResponseDto>> CreatePairingTicketAsync(CreatePairingTicketDto dto, int currentUserId, bool isAdmin = false)
    {
        var child = await _context.Children.FirstOrDefaultAsync(c => c.Id == dto.ChildId);
        if (child == null)
        {
            return ServiceResult<PairingTicketResponseDto>.NotFound($"Child with ID {dto.ChildId} was not found.");
        }

        if (!isAdmin && child.ParentId != currentUserId)
        {
            return ServiceResult<PairingTicketResponseDto>.NotFound($"Child with ID {dto.ChildId} was not found or does not belong to your account.");
        }

        // Generate cryptographically secure random TicketId (32 hex characters)
        var ticketIdBytes = RandomNumberGenerator.GetBytes(16);
        var ticketId = Convert.ToHexString(ticketIdBytes).ToLowerInvariant();

        // Generate 8-character human-friendly PairingCode, e.g. "PAIR-7X9K"
        const string codeChars = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"; // exclude ambiguous 0, 1, O, I
        var randomCode = new char[4];
        var rngBytes = RandomNumberGenerator.GetBytes(4);
        for (int i = 0; i < 4; i++)
        {
            randomCode[i] = codeChars[rngBytes[i] % codeChars.Length];
        }
        var pairingCode = $"PAIR-{new string(randomCode)}";

        // Ticket validity (default 600 seconds / 10 minutes)
        var validitySeconds = dto.ValiditySeconds.HasValue ? Math.Clamp(dto.ValiditySeconds.Value, 1, 86400) : 600;
        var expiresAt = DateTime.UtcNow.AddSeconds(validitySeconds);

        var ticket = new PairingTicket
        {
            TicketId = ticketId,
            PairingCode = pairingCode,
            ChildId = child.Id,
            ParentId = child.ParentId,
            ExpiresAt = expiresAt,
            IsConsumed = false
        };

        // Cache retention includes a grace window so expired tickets can be explicitly identified
        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = expiresAt.AddMinutes(30)
        };

        _cache.Set($"pair_ticket_{ticketId}", ticket, cacheEntryOptions);
        _cache.Set($"pair_code_{pairingCode}", ticket, cacheEntryOptions);

        var qrPayloadObj = new
        {
            ticketId,
            pairingCode,
            childId = child.Id,
            childName = child.FullName,
            expiresAt
        };
        var qrPayload = JsonSerializer.Serialize(qrPayloadObj);

        var response = new PairingTicketResponseDto
        {
            TicketId = ticketId,
            PairingCode = pairingCode,
            QrPayload = qrPayload,
            ChildId = child.Id,
            ChildName = child.FullName,
            ExpiresAt = expiresAt
        };

        return ServiceResult<PairingTicketResponseDto>.Success(response);
    }

    /// <summary>
    /// Enrolls a child device using a valid single-use pairing ticket.
    /// Atomically consumes the ticket and issues a specialized ChildDevice JWT.
    /// </summary>
    public async Task<ServiceResult<EnrollmentResponseDto>> EnrollDeviceAsync(DeviceEnrollmentDto dto)
    {
        var rawCode = dto.PairingCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawCode))
        {
            return ServiceResult<EnrollmentResponseDto>.BadRequest("Pairing code or ticket is required.");
        }

        PairingTicket? ticket = null;

        // 1. Try parsing JSON if rawCode contains QR JSON payload
        if (rawCode.StartsWith("{") && rawCode.EndsWith("}"))
        {
            try
            {
                using var jsonDoc = JsonDocument.Parse(rawCode);
                if (jsonDoc.RootElement.TryGetProperty("ticketId", out var tidProp))
                {
                    var tid = tidProp.GetString();
                    if (!string.IsNullOrEmpty(tid))
                    {
                        _cache.TryGetValue($"pair_ticket_{tid}", out ticket);
                    }
                }
                if (ticket == null && jsonDoc.RootElement.TryGetProperty("pairingCode", out var pcodeProp))
                {
                    var pcode = pcodeProp.GetString();
                    if (!string.IsNullOrEmpty(pcode))
                    {
                        _cache.TryGetValue($"pair_code_{pcode.ToUpperInvariant()}", out ticket);
                    }
                }
            }
            catch
            {
                // Not valid JSON, continue with raw string
            }
        }

        // 2. Direct lookup by ticketId or pairingCode
        if (ticket == null)
        {
            if (!_cache.TryGetValue($"pair_ticket_{rawCode}", out ticket))
            {
                _cache.TryGetValue($"pair_code_{rawCode.ToUpperInvariant()}", out ticket);
            }
        }

        if (ticket == null)
        {
            return ServiceResult<EnrollmentResponseDto>.BadRequest("Invalid or non-existent pairing ticket.");
        }

        // 3. Expiration check
        if (ticket.ExpiresAt <= DateTime.UtcNow)
        {
            _cache.Remove($"pair_ticket_{ticket.TicketId}");
            _cache.Remove($"pair_code_{ticket.PairingCode}");
            return ServiceResult<EnrollmentResponseDto>.BadRequest("Pairing ticket has expired.");
        }

        // 4. Thread-safe atomic single-use check
        lock (ticket.Lock)
        {
            if (ticket.IsConsumed)
            {
                return ServiceResult<EnrollmentResponseDto>.BadRequest("Pairing ticket has already been used.");
            }
            ticket.IsConsumed = true;
        }

        // Purge immediately from cache so subsequent requests fail
        _cache.Remove($"pair_ticket_{ticket.TicketId}");
        _cache.Remove($"pair_code_{ticket.PairingCode}");

        var child = await _context.Children.FirstOrDefaultAsync(c => c.Id == ticket.ChildId);
        if (child == null)
        {
            return ServiceResult<EnrollmentResponseDto>.BadRequest("Linked child profile no longer exists.");
        }

        var trimmedDeviceName = dto.DeviceName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedDeviceName))
        {
            trimmedDeviceName = $"{child.FullName}'s Device";
        }

        var identifier = dto.DeviceIdentifier?.Trim();
        Device? device = null;
        if (!string.IsNullOrEmpty(identifier))
        {
            device = await _context.Devices.FirstOrDefaultAsync(d => d.ChildId == child.Id && d.DeviceIdentifier == identifier);
        }

        if (device == null)
        {
            device = new Device
            {
                DeviceName = trimmedDeviceName,
                DeviceIdentifier = identifier,
                Platform = dto.Platform?.Trim() ?? "Android",
                ChildId = child.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.Devices.Add(device);
        }
        else
        {
            device.DeviceName = trimmedDeviceName;
            device.Platform = dto.Platform?.Trim() ?? device.Platform;
            device.IsActive = true;
        }

        await _context.SaveChangesAsync();

        var deviceToken = _jwtTokenService.GenerateDeviceToken(device, child, out var tokenExpiresAt);

        var response = new EnrollmentResponseDto
        {
            DeviceToken = deviceToken,
            DeviceId = device.Id,
            ChildId = child.Id,
            ChildName = child.FullName,
            ExpiresAt = tokenExpiresAt
        };

        return ServiceResult<EnrollmentResponseDto>.Success(response);
    }

    /// <summary>
    /// Retrieves active monitoring rules for the enrolled child device.
    /// </summary>
    public async Task<ServiceResult<IEnumerable<MonitoringRuleResponseDto>>> GetMyRulesAsync(int childId)
    {
        var rules = await _context.MonitoringRules
            .AsNoTracking()
            .Where(r => r.ChildId == childId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new MonitoringRuleResponseDto
            {
                Id = r.Id,
                RuleName = r.RuleName,
                RuleType = r.RuleType,
                Description = r.Description,
                IsEnabled = r.IsEnabled,
                RuleConfig = r.RuleConfig,
                Version = r.Version,
                SyncStatus = r.SyncStatus,
                LastModifiedAt = r.LastModifiedAt,
                ChildId = r.ChildId,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        return ServiceResult<IEnumerable<MonitoringRuleResponseDto>>.Success(rules);
    }

    /// <summary>
    /// Acknowledges rule synchronization status from an enrolled child device.
    /// </summary>
    public async Task<ServiceResult<bool>> AcknowledgeRuleSyncAsync(int childId, BatchRuleSyncAckDto dto)
    {
        if (dto.Acks == null || dto.Acks.Count == 0)
        {
            return ServiceResult<bool>.Success(true);
        }

        var ruleIds = dto.Acks.Select(a => a.RuleId).Distinct().ToList();
        var rules = await _context.MonitoringRules
            .Where(r => r.ChildId == childId && ruleIds.Contains(r.Id))
            .ToListAsync();

        var ackMap = dto.Acks.GroupBy(a => a.RuleId).ToDictionary(g => g.Key, g => g.Last());

        foreach (var rule in rules)
        {
            if (ackMap.TryGetValue(rule.Id, out var ack))
            {
                rule.SyncStatus = ack.Status?.Trim() ?? "SyncedToDevice";
            }
        }

        await _context.SaveChangesAsync();
        return ServiceResult<bool>.Success(true);
    }

    /// <summary>
    /// Updates battery percentage and charging state for an enrolled child device.
    /// </summary>
    public async Task<ServiceResult<DeviceResponseDto>> UpdateBatteryStatusAsync(int deviceId, UpdateBatteryDto dto)
    {
        var device = await _context.Devices.FindAsync(deviceId);
        if (device == null)
        {
            return ServiceResult<DeviceResponseDto>.NotFound($"Device with ID {deviceId} was not found.");
        }

        if (!device.IsActive)
        {
            return ServiceResult<DeviceResponseDto>.BadRequest("Device is inactive or has been revoked.");
        }

        device.BatteryPercentage = dto.BatteryPercentage;
        device.IsCharging = dto.IsCharging;
        device.BatteryUpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ServiceResult<DeviceResponseDto>.Success(new DeviceResponseDto
        {
            Id = device.Id,
            DeviceName = device.DeviceName,
            DeviceIdentifier = device.DeviceIdentifier,
            Platform = device.Platform,
            ChildId = device.ChildId,
            IsActive = device.IsActive,
            BatteryPercentage = device.BatteryPercentage,
            IsCharging = device.IsCharging,
            BatteryUpdatedAt = device.BatteryUpdatedAt,
            CreatedAt = device.CreatedAt
        });
    }

    /// <summary>
    /// Creates a mode change or unlinking request from a child device.
    /// Prevents duplicate active pending requests.
    /// </summary>
    public async Task<ServiceResult<DeviceModeRequestResponseDto>> CreateModeRequestAsync(int deviceId, int childId, CreateModeRequestDto dto)
    {
        var device = await _context.Devices
            .Include(d => d.Child)
            .FirstOrDefaultAsync(d => d.Id == deviceId);

        if (device == null || device.ChildId != childId)
        {
            return ServiceResult<DeviceModeRequestResponseDto>.NotFound("Thiết bị không tồn tại hoặc không khớp với hồ sơ trẻ.");
        }

        if (!device.IsActive)
        {
            return ServiceResult<DeviceModeRequestResponseDto>.BadRequest("Thiết bị đã bị vô hiệu hóa.");
        }

        var nowUtc = DateTime.UtcNow;

        // Check if there is an active pending request already (within validity window)
        var existingRequest = await _context.DeviceModeRequests
            .Include(r => r.Device)
            .Include(r => r.Child)
            .Include(r => r.ReviewedByUser)
            .FirstOrDefaultAsync(r => r.DeviceId == deviceId && r.Status == "Pending" && r.ExpiresAt > nowUtc);

        if (existingRequest != null)
        {
            return ServiceResult<DeviceModeRequestResponseDto>.Success(MapToModeRequestDto(existingRequest));
        }

        var newRequest = new DeviceModeRequest
        {
            DeviceId = deviceId,
            ChildId = childId,
            TargetMode = dto.TargetMode,
            Reason = dto.Reason?.Trim(),
            Status = "Pending",
            RequestedAt = nowUtc,
            ExpiresAt = nowUtc.AddMinutes(15)
        };

        _context.DeviceModeRequests.Add(newRequest);
        await _context.SaveChangesAsync();

        return ServiceResult<DeviceModeRequestResponseDto>.Success(MapToModeRequestDto(newRequest, device.DeviceName, device.Child?.FullName ?? string.Empty));
    }

    /// <summary>
    /// Retrieves all pending mode change requests for children belonging to the parent.
    /// </summary>
    public async Task<ServiceResult<IEnumerable<DeviceModeRequestResponseDto>>> GetPendingModeRequestsForParentAsync(int parentId, bool isAdmin = false)
    {
        var nowUtc = DateTime.UtcNow;
        var query = _context.DeviceModeRequests
            .AsNoTracking()
            .Include(r => r.Device)
            .Include(r => r.Child)
            .Include(r => r.ReviewedByUser)
            .Where(r => r.Status == "Pending" && r.ExpiresAt > nowUtc);

        if (!isAdmin)
        {
            query = query.Where(r => r.Child.ParentId == parentId);
        }

        var records = await query
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync();

        var list = records
            .Select(r => MapToModeRequestDto(r, r.Device?.DeviceName, r.Child?.FullName, r.ReviewedByUser?.FullName))
            .ToList();

        return ServiceResult<IEnumerable<DeviceModeRequestResponseDto>>.Success(list);
    }

    /// <summary>
    /// Retrieves the active pending mode change request for a specific child device.
    /// </summary>
    public async Task<ServiceResult<DeviceModeRequestResponseDto?>> GetPendingModeRequestForDeviceAsync(int deviceId)
    {
        var nowUtc = DateTime.UtcNow;
        var request = await _context.DeviceModeRequests
            .AsNoTracking()
            .Include(r => r.Device)
            .Include(r => r.Child)
            .Include(r => r.ReviewedByUser)
            .Where(r => r.DeviceId == deviceId && r.Status == "Pending" && r.ExpiresAt > nowUtc)
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync();

        if (request == null)
        {
            return ServiceResult<DeviceModeRequestResponseDto?>.Success(null);
        }

        return ServiceResult<DeviceModeRequestResponseDto?>.Success(MapToModeRequestDto(request));
    }

    /// <summary>
    /// Retrieves mode request details by ID with authorization checks.
    /// </summary>
    public async Task<ServiceResult<DeviceModeRequestResponseDto>> GetModeRequestByIdAsync(int requestId, int currentUserId, bool isChildDevice, int childId = 0, bool isAdmin = false)
    {
        var request = await _context.DeviceModeRequests
            .Include(r => r.Device)
            .Include(r => r.Child)
            .Include(r => r.ReviewedByUser)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null)
        {
            return ServiceResult<DeviceModeRequestResponseDto>.NotFound($"Yêu cầu {requestId} không tồn tại.");
        }

        // Authorization check
        if (isChildDevice)
        {
            if (request.DeviceId != currentUserId && request.ChildId != childId)
            {
                return ServiceResult<DeviceModeRequestResponseDto>.NotFound($"Yêu cầu {requestId} không tồn tại hoặc không thuộc thiết bị của bạn.");
            }
        }
        else if (!isAdmin && request.Child.ParentId != currentUserId)
        {
            return ServiceResult<DeviceModeRequestResponseDto>.NotFound($"Yêu cầu {requestId} không tồn tại hoặc không thuộc quyền quản lý của bạn.");
        }

        // Auto-expire if past expiration
        var nowUtc = DateTime.UtcNow;
        if (request.Status == "Pending" && request.ExpiresAt <= nowUtc)
        {
            request.Status = "Expired";
            await _context.SaveChangesAsync();
        }

        return ServiceResult<DeviceModeRequestResponseDto>.Success(MapToModeRequestDto(request));
    }

    /// <summary>
    /// Approves a child device mode request (restricted to parent owning the child).
    /// </summary>
    public async Task<ServiceResult<DeviceModeRequestResponseDto>> ApproveModeRequestAsync(int requestId, int parentId, bool isAdmin = false)
    {
        var request = await _context.DeviceModeRequests
            .Include(r => r.Device)
            .Include(r => r.Child)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null)
        {
            return ServiceResult<DeviceModeRequestResponseDto>.NotFound($"Yêu cầu {requestId} không tồn tại.");
        }

        if (!isAdmin && request.Child.ParentId != parentId)
        {
            return ServiceResult<DeviceModeRequestResponseDto>.NotFound($"Yêu cầu {requestId} không tồn tại hoặc không thuộc quyền quản lý của bạn.");
        }

        var nowUtc = DateTime.UtcNow;
        if (request.Status != "Pending")
        {
            return ServiceResult<DeviceModeRequestResponseDto>.BadRequest($"Yêu cầu đã ở trạng thái '{request.Status}', không thể phê duyệt lại.");
        }

        if (request.ExpiresAt <= nowUtc)
        {
            request.Status = "Expired";
            await _context.SaveChangesAsync();
            return ServiceResult<DeviceModeRequestResponseDto>.BadRequest("Yêu cầu đã hết hạn và không thể phê duyệt.");
        }

        var reviewer = await _context.Users.FindAsync(parentId);
        var token = Guid.NewGuid().ToString("N");

        request.Status = "Approved";
        request.ApprovalToken = token;
        request.ReviewedAt = nowUtc;
        request.ReviewedByUserId = parentId;

        await _context.SaveChangesAsync();

        return ServiceResult<DeviceModeRequestResponseDto>.Success(MapToModeRequestDto(request, request.Device.DeviceName, request.Child.FullName, reviewer?.FullName));
    }

    /// <summary>
    /// Rejects a child device mode request (restricted to parent owning the child).
    /// </summary>
    public async Task<ServiceResult<DeviceModeRequestResponseDto>> RejectModeRequestAsync(int requestId, int parentId, bool isAdmin = false)
    {
        var request = await _context.DeviceModeRequests
            .Include(r => r.Device)
            .Include(r => r.Child)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null)
        {
            return ServiceResult<DeviceModeRequestResponseDto>.NotFound($"Yêu cầu {requestId} không tồn tại.");
        }

        if (!isAdmin && request.Child.ParentId != parentId)
        {
            return ServiceResult<DeviceModeRequestResponseDto>.NotFound($"Yêu cầu {requestId} không tồn tại hoặc không thuộc quyền quản lý của bạn.");
        }

        if (request.Status != "Pending")
        {
            return ServiceResult<DeviceModeRequestResponseDto>.BadRequest($"Yêu cầu đã ở trạng thái '{request.Status}', không thể từ chối.");
        }

        var nowUtc = DateTime.UtcNow;
        var reviewer = await _context.Users.FindAsync(parentId);

        request.Status = "Rejected";
        request.ReviewedAt = nowUtc;
        request.ReviewedByUserId = parentId;

        await _context.SaveChangesAsync();

        return ServiceResult<DeviceModeRequestResponseDto>.Success(MapToModeRequestDto(request, request.Device.DeviceName, request.Child.FullName, reviewer?.FullName));
    }

    /// <summary>
    /// Executes an approved mode change or unlink on the child device.
    /// </summary>
    public async Task<ServiceResult<bool>> ExecuteModeChangeAsync(int deviceId, int requestId, ExecuteModeChangeDto dto)
    {
        var request = await _context.DeviceModeRequests
            .Include(r => r.Device)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null || request.DeviceId != deviceId)
        {
            return ServiceResult<bool>.NotFound("Yêu cầu không tồn tại hoặc không khớp với thiết bị hiện tại.");
        }

        if (request.Status != "Approved")
        {
            return ServiceResult<bool>.BadRequest($"Yêu cầu chưa được phê duyệt hợp lệ (Trạng thái hiện tại: {request.Status}).");
        }

        if (string.IsNullOrWhiteSpace(dto.ApprovalToken) || request.ApprovalToken != dto.ApprovalToken.Trim())
        {
            return ServiceResult<bool>.BadRequest("Mã phê duyệt không chính xác hoặc đã bị vô hiệu hóa.");
        }

        var nowUtc = DateTime.UtcNow;
        if (request.ExpiresAt <= nowUtc)
        {
            request.Status = "Expired";
            await _context.SaveChangesAsync();
            return ServiceResult<bool>.BadRequest("Phiên phê duyệt đã hết hạn. Vui lòng yêu cầu lại.");
        }

        request.Status = "Executed";
        request.ExecutedAt = nowUtc;
        request.ApprovalToken = null; // Single-use consumption

        if (request.TargetMode == "Unlink")
        {
            request.Device.IsActive = false;
        }

        await _context.SaveChangesAsync();
        return ServiceResult<bool>.Success(true);
    }

    /// <summary>
    /// Directly validates parent credentials on child device for immediate unlock/unlink.
    /// </summary>
    public async Task<ServiceResult<DeviceModeRequestResponseDto>> DirectParentUnlockAsync(int deviceId, int childId, DirectParentUnlockDto dto)
    {
        var device = await _context.Devices
            .Include(d => d.Child)
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.ChildId == childId);

        if (device == null)
        {
            return ServiceResult<DeviceModeRequestResponseDto>.NotFound("Thiết bị không tồn tại.");
        }

        var parent = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == dto.Email.Trim().ToLower());
        if (parent == null || parent.Id != device.Child.ParentId)
        {
            return ServiceResult<DeviceModeRequestResponseDto>.BadRequest("Email này không phải của phụ huynh quản lý thiết bị này.");
        }

        if (string.IsNullOrWhiteSpace(parent.PasswordHash) || !VerifyPassword(dto.Password, parent.PasswordHash))
        {
            return ServiceResult<DeviceModeRequestResponseDto>.BadRequest("Mật khẩu phụ huynh không chính xác.");
        }

        var nowUtc = DateTime.UtcNow;
        var token = Guid.NewGuid().ToString("N");

        var auditRequest = new DeviceModeRequest
        {
            DeviceId = deviceId,
            ChildId = childId,
            TargetMode = dto.TargetMode,
            Reason = "Phụ huynh mở khóa trực tiếp bằng mật khẩu trên thiết bị",
            Status = "Executed",
            ApprovalToken = null,
            RequestedAt = nowUtc,
            ExpiresAt = nowUtc.AddMinutes(15),
            ReviewedAt = nowUtc,
            ReviewedByUserId = parent.Id,
            ExecutedAt = nowUtc
        };

        if (dto.TargetMode == "Unlink")
        {
            device.IsActive = false;
        }

        _context.DeviceModeRequests.Add(auditRequest);
        await _context.SaveChangesAsync();

        return ServiceResult<DeviceModeRequestResponseDto>.Success(new DeviceModeRequestResponseDto
        {
            Id = auditRequest.Id,
            DeviceId = device.Id,
            DeviceName = device.DeviceName,
            ChildId = device.ChildId,
            ChildName = device.Child.FullName,
            TargetMode = auditRequest.TargetMode,
            Reason = auditRequest.Reason,
            Status = auditRequest.Status,
            ApprovalToken = token,
            RequestedAt = auditRequest.RequestedAt,
            ExpiresAt = auditRequest.ExpiresAt,
            ReviewedAt = auditRequest.ReviewedAt,
            ReviewedByName = parent.FullName,
            ExecutedAt = auditRequest.ExecutedAt
        });
    }

    private static DeviceModeRequestResponseDto MapToModeRequestDto(DeviceModeRequest r, string? deviceName = null, string? childName = null, string? reviewerName = null)
    {
        return new DeviceModeRequestResponseDto
        {
            Id = r.Id,
            DeviceId = r.DeviceId,
            DeviceName = deviceName ?? r.Device?.DeviceName ?? string.Empty,
            ChildId = r.ChildId,
            ChildName = childName ?? r.Child?.FullName ?? string.Empty,
            TargetMode = r.TargetMode,
            Reason = r.Reason,
            Status = r.Status,
            ApprovalToken = r.ApprovalToken,
            RequestedAt = r.RequestedAt,
            ExpiresAt = r.ExpiresAt,
            ReviewedAt = r.ReviewedAt,
            ReviewedByName = reviewerName ?? r.ReviewedByUser?.FullName,
            ExecutedAt = r.ExecutedAt
        };
    }

    private static bool VerifyPassword(string password, string storedPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(storedPasswordHash) || !storedPasswordHash.Contains(':'))
        {
            return false;
        }

        var parts = storedPasswordHash.Split(':');
        if (parts.Length != 2) return false;

        try
        {
            var salt = Convert.FromBase64String(parts[0]);
            var expectedHash = Convert.FromBase64String(parts[1]);

            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                System.Text.Encoding.UTF8.GetBytes(password),
                salt,
                iterations: 100000,
                HashAlgorithmName.SHA256,
                outputLength: 32);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }
}
