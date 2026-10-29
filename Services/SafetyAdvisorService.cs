using Microsoft.EntityFrameworkCore;
using tran_nguyen_vi_khang_api.Data;
using tran_nguyen_vi_khang_api.DTOs.Common;
using tran_nguyen_vi_khang_api.DTOs.SafetyAdvisor;
using tran_nguyen_vi_khang_api.Services.Interfaces;

namespace tran_nguyen_vi_khang_api.Services;

/// <summary>
/// Evaluates child monitoring telemetry (battery levels, charging status, location recency)
/// and provides deterministic, actionable safety advice for parents.
/// 
/// Note on Rule C and Schedule Violations:
/// The application schema supports MonitoringRule definitions (TimeLimit, AppBlock, etc.)
/// but does not record app usage telemetry, web browsing logs, or schedule violations.
/// To preserve system integrity and avoid fabricating false alerts, active schedule
/// and app violations are omitted until telemetry ingestion is implemented.
/// </summary>
public class SafetyAdvisorService : ISafetyAdvisorService
{
    private readonly AppDbContext _context;

    public SafetyAdvisorService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceResult<SafetyAdvisorResponseDto>> GetSafetyAdvisorAsync(
        int childId,
        int currentUserId,
        bool isAdmin,
        DateTime? evaluationTimeUtc = null)
    {
        var now = evaluationTimeUtc ?? DateTime.UtcNow;

        // 1. Verify Child existence and strict Parent ownership (or Admin)
        var childQuery = _context.Children
            .AsNoTracking()
            .Include(c => c.Devices)
            .Where(c => c.Id == childId);

        if (!isAdmin)
        {
            childQuery = childQuery.Where(c => c.ParentId == currentUserId);
        }

        var child = await childQuery.FirstOrDefaultAsync();
        if (child == null)
        {
            return ServiceResult<SafetyAdvisorResponseDto>.NotFound($"Child with ID {childId} was not found.");
        }

        // 2. Fetch latest GPS location for this child
        var latestLocation = await _context.DeviceLocations
            .AsNoTracking()
            .Include(dl => dl.Device)
            .Where(dl => dl.ChildId == childId)
            .OrderByDescending(dl => dl.RecordedAt)
            .ThenByDescending(dl => dl.Id)
            .FirstOrDefaultAsync();

        var allDevices = child.Devices.OrderByDescending(d => d.CreatedAt).ToList();
        var activeDevices = allDevices.Where(d => d.IsActive).ToList();

        var warnings = new List<SafetyWarningDto>();
        var deviceSummaries = new List<SafetyDeviceSummaryDto>();

        // 3. Rule A & C: Evaluate Device Battery & Hardware Status
        if (activeDevices.Count == 0)
        {
            warnings.Add(new SafetyWarningDto
            {
                Type = "NoDevice",
                Severity = "NeedsAttention",
                Title = "Chưa có thiết bị kết nối",
                Message = "Hồ sơ của trẻ chưa có thiết bị đang hoạt động nào được kết nối với hệ thống.",
                Reading = "0 thiết bị",
                Timestamp = null,
                SuggestedAction = "Tạo mã QR ghép nối để liên kết thiết bị của trẻ với ứng dụng.",
                ActionType = "PairDevice"
            });
        }
        else
        {
            foreach (var dev in activeDevices)
            {
                var isStaleBattery = dev.BatteryUpdatedAt.HasValue &&
                                     (now - dev.BatteryUpdatedAt.Value).TotalHours > 12;

                deviceSummaries.Add(new SafetyDeviceSummaryDto
                {
                    DeviceId = dev.Id,
                    DeviceName = dev.DeviceName,
                    IsActive = dev.IsActive,
                    BatteryPercentage = dev.BatteryPercentage,
                    IsCharging = dev.IsCharging,
                    BatteryUpdatedAt = dev.BatteryUpdatedAt,
                    IsStaleBattery = isStaleBattery
                });

                // Rule A: Missing battery vs Low battery vs Stale battery
                if (!dev.BatteryPercentage.HasValue)
                {
                    warnings.Add(new SafetyWarningDto
                    {
                        Type = "MissingBattery",
                        Severity = "NeedsAttention",
                        Title = "Chưa có dữ liệu pin",
                        Message = $"Thiết bị '{dev.DeviceName}' chưa gửi dữ liệu pin về hệ thống.",
                        Reading = "Chưa có dữ liệu",
                        Timestamp = null,
                        SuggestedAction = "Mở ứng dụng trên điện thoại của trẻ để cấp quyền và đồng bộ tình trạng pin.",
                        ActionType = "ContactChild",
                        DeviceId = dev.Id,
                        DeviceName = dev.DeviceName
                    });
                }
                else
                {
                    // Check stale battery reading (> 12 hours)
                    if (isStaleBattery)
                    {
                        warnings.Add(new SafetyWarningDto
                        {
                            Type = "StaleBattery",
                            Severity = "NeedsAttention",
                            Title = "Dữ liệu pin có thể đã cũ",
                            Message = $"Thông tin pin của '{dev.DeviceName}' được cập nhật lúc {dev.BatteryUpdatedAt!.Value:HH:mm dd/MM/yyyy} UTC, có thể không phản ánh mức pin hiện tại.",
                            Reading = $"{dev.BatteryPercentage}% (cũ)",
                            Timestamp = dev.BatteryUpdatedAt,
                            SuggestedAction = "Nhắc nhở trẻ mở ứng dụng hoặc kiểm tra kết nối mạng của thiết bị.",
                            ActionType = "ContactChild",
                            DeviceId = dev.Id,
                            DeviceName = dev.DeviceName
                        });
                    }

                    // Check low battery (< 20%)
                    if (dev.BatteryPercentage.Value < 20)
                    {
                        if (dev.IsCharging == true)
                        {
                            warnings.Add(new SafetyWarningDto
                            {
                                Type = "LowBattery",
                                Severity = "NeedsAttention",
                                Title = "Pin thấp (đang sạc)",
                                Message = $"Thiết bị '{dev.DeviceName}' có mức pin yếu ({dev.BatteryPercentage}%), nhưng hiện đang được cắm sạc.",
                                Reading = $"{dev.BatteryPercentage}% (Đang sạc)",
                                Timestamp = dev.BatteryUpdatedAt,
                                SuggestedAction = "Nhắc trẻ tiếp tục sạc đầy pin trước khi rời khỏi nhà hoặc khu vực an toàn.",
                                ActionType = "ChargeDevice",
                                DeviceId = dev.Id,
                                DeviceName = dev.DeviceName
                            });
                        }
                        else
                        {
                            warnings.Add(new SafetyWarningDto
                            {
                                Type = "LowBattery",
                                Severity = "Warning",
                                Title = "Cảnh báo pin yếu",
                                Message = $"Thiết bị '{dev.DeviceName}' chỉ còn {dev.BatteryPercentage}% pin và chưa được cắm sạc.",
                                Reading = $"{dev.BatteryPercentage}%",
                                Timestamp = dev.BatteryUpdatedAt,
                                SuggestedAction = "Liên hệ ngay với trẻ để nhắc nhở cắm sạc hoặc mang theo sạc dự phòng.",
                                ActionType = "ContactChild",
                                DeviceId = dev.Id,
                                DeviceName = dev.DeviceName
                            });
                        }
                    }
                }
            }
        }

        // 4. Rule B: Evaluate Location Recency
        SafetyLocationSummaryDto? locationSummary = null;
        if (latestLocation == null)
        {
            if (activeDevices.Count > 0)
            {
                warnings.Add(new SafetyWarningDto
                {
                    Type = "NoLocation",
                    Severity = "NeedsAttention",
                    Title = "Chưa có dữ liệu vị trí",
                    Message = "Hệ thống chưa nhận được toạ độ GPS nào từ thiết bị của trẻ.",
                    Reading = "Chưa có toạ độ",
                    Timestamp = null,
                    SuggestedAction = "Gửi yêu cầu làm mới vị trí hoặc kiểm tra quyền GPS trên máy của trẻ.",
                    ActionType = "RefreshLocation"
                });
            }
        }
        else
        {
            var ageSpan = now - latestLocation.RecordedAt;
            var ageMinutes = Math.Max(0, (int)ageSpan.TotalMinutes);
            var isStaleLocation = ageMinutes > 30;

            locationSummary = new SafetyLocationSummaryDto
            {
                LocationId = latestLocation.Id,
                Latitude = latestLocation.Latitude,
                Longitude = latestLocation.Longitude,
                Accuracy = latestLocation.Accuracy,
                RecordedAt = latestLocation.RecordedAt,
                IsStaleLocation = isStaleLocation,
                AgeMinutes = ageMinutes
            };

            if (isStaleLocation)
            {
                warnings.Add(new SafetyWarningDto
                {
                    Type = "StaleLocation",
                    Severity = "NeedsAttention",
                    Title = "Vị trí chưa được cập nhật",
                    Message = $"Vị trí GPS được ghi nhận lần cuối lúc {latestLocation.RecordedAt:HH:mm dd/MM/yyyy} UTC ({ageMinutes} phút trước). Dữ liệu vị trí có thể không còn cập nhật.",
                    Reading = $"{ageMinutes} phút trước",
                    Timestamp = latestLocation.RecordedAt,
                    SuggestedAction = "Nhấn 'Làm mới vị trí' để yêu cầu thiết bị gửi lại toạ độ GPS mới nhất.",
                    ActionType = "RefreshLocation",
                    DeviceId = latestLocation.DeviceId,
                    DeviceName = latestLocation.Device?.DeviceName
                });
            }
        }

        // Primary device: the one with known battery or first active device
        var primaryDeviceSummary = deviceSummaries.FirstOrDefault(d => d.HasBatteryData)
                                   ?? deviceSummaries.FirstOrDefault();

        // 5. Data Sufficiency Check:
        // Essential monitoring requires at least 1 active device AND (known battery data OR known location).
        var hasSufficientData = activeDevices.Count > 0 &&
                                (activeDevices.Any(d => d.BatteryPercentage.HasValue) || latestLocation != null);

        // 6. Overall Safety Status Determination
        string overallStatus;
        string statusTitle;
        string statusMessage;

        if (!hasSufficientData)
        {
            overallStatus = "InsufficientData";
            statusTitle = "Chưa đủ dữ liệu";
            statusMessage = "Chưa có đủ dữ liệu giám sát từ thiết bị của trẻ để đưa ra đánh giá an toàn.";
        }
        else if (warnings.Any(w => w.Severity == "Warning"))
        {
            overallStatus = "Warning";
            statusTitle = "Cảnh báo an toàn";
            statusMessage = "Phát hiện vấn đề an toàn cần phụ huynh xử lý sớm.";
        }
        else if (warnings.Any(w => w.Severity == "NeedsAttention"))
        {
            overallStatus = "NeedsAttention";
            statusTitle = "Cần lưu ý";
            statusMessage = "Thiết bị có một số trạng thái cần phụ huynh lưu ý.";
        }
        else
        {
            overallStatus = "Safe";
            statusTitle = "An toàn dựa trên dữ liệu hiện có";
            statusMessage = "Không phát hiện cảnh báo nào dựa trên dữ liệu hiện tại.";
        }

        var response = new SafetyAdvisorResponseDto
        {
            ChildId = child.Id,
            ChildName = child.FullName,
            OverallStatus = overallStatus,
            StatusTitle = statusTitle,
            StatusMessage = statusMessage,
            EvaluatedAt = now,
            HasSufficientData = hasSufficientData,
            TotalDevices = allDevices.Count,
            ActiveDevices = activeDevices.Count,
            PrimaryDevice = primaryDeviceSummary,
            Devices = deviceSummaries,
            LatestLocation = locationSummary,
            Warnings = warnings
        };

        return ServiceResult<SafetyAdvisorResponseDto>.Success(response);
    }
}
