using tran_nguyen_vi_khang_api.DTOs.Common;
using tran_nguyen_vi_khang_api.DTOs.Device;
using tran_nguyen_vi_khang_api.DTOs.MonitoringRule;

namespace tran_nguyen_vi_khang_api.Services.Interfaces;

public interface IDeviceService
{
    Task<IEnumerable<DeviceResponseDto>> GetAllAsync(int currentUserId, bool isAdmin = false, int? childId = null);
    Task<DeviceResponseDto?> GetByIdAsync(int id, int currentUserId, bool isAdmin = false);
    Task<ServiceResult<DeviceResponseDto>> CreateAsync(CreateDeviceDto dto, int currentUserId, bool isAdmin = false);
    Task<ServiceResult<DeviceResponseDto>> UpdateAsync(int id, UpdateDeviceDto dto, int currentUserId, bool isAdmin = false);
    Task<ServiceResult<bool>> DeleteAsync(int id, int currentUserId, bool isAdmin = false);

    /// <summary>
    /// Generates a single-use pairing ticket for a parent to enroll a child device via QR code.
    /// </summary>
    Task<ServiceResult<PairingTicketResponseDto>> CreatePairingTicketAsync(CreatePairingTicketDto dto, int currentUserId, bool isAdmin = false);

    /// <summary>
    /// Enrolls a child device using a valid single-use pairing ticket.
    /// </summary>
    Task<ServiceResult<EnrollmentResponseDto>> EnrollDeviceAsync(DeviceEnrollmentDto dto);

    /// <summary>
    /// Retrieves active monitoring rules for the enrolled child device.
    /// </summary>
    Task<ServiceResult<IEnumerable<MonitoringRuleResponseDto>>> GetMyRulesAsync(int childId);

    /// <summary>
    /// Acknowledges rule synchronization status from an enrolled child device.
    /// </summary>
    Task<ServiceResult<bool>> AcknowledgeRuleSyncAsync(int childId, BatchRuleSyncAckDto dto);

    /// <summary>
    /// Updates battery percentage and charging state for an enrolled child device.
    /// </summary>
    Task<ServiceResult<DeviceResponseDto>> UpdateBatteryStatusAsync(int deviceId, UpdateBatteryDto dto);

    /// <summary>
    /// Creates a mode change or unlinking request from a child device.
    /// </summary>
    Task<ServiceResult<DeviceModeRequestResponseDto>> CreateModeRequestAsync(int deviceId, int childId, CreateModeRequestDto dto);

    /// <summary>
    /// Retrieves all pending mode change requests for children belonging to the parent.
    /// </summary>
    Task<ServiceResult<IEnumerable<DeviceModeRequestResponseDto>>> GetPendingModeRequestsForParentAsync(int parentId, bool isAdmin = false);

    /// <summary>
    /// Retrieves the pending mode change request for a specific child device.
    /// </summary>
    Task<ServiceResult<DeviceModeRequestResponseDto?>> GetPendingModeRequestForDeviceAsync(int deviceId);

    /// <summary>
    /// Retrieves mode request details by ID with authorization checks.
    /// </summary>
    Task<ServiceResult<DeviceModeRequestResponseDto>> GetModeRequestByIdAsync(int requestId, int currentUserId, bool isChildDevice, int childId = 0, bool isAdmin = false);

    /// <summary>
    /// Approves a child device mode request (restricted to parent owning the child).
    /// </summary>
    Task<ServiceResult<DeviceModeRequestResponseDto>> ApproveModeRequestAsync(int requestId, int parentId, bool isAdmin = false);

    /// <summary>
    /// Rejects a child device mode request (restricted to parent owning the child).
    /// </summary>
    Task<ServiceResult<DeviceModeRequestResponseDto>> RejectModeRequestAsync(int requestId, int parentId, bool isAdmin = false);

    /// <summary>
    /// Executes an approved mode change or unlink on the child device.
    /// </summary>
    Task<ServiceResult<bool>> ExecuteModeChangeAsync(int deviceId, int requestId, ExecuteModeChangeDto dto);

    /// <summary>
    /// Directly validates parent credentials on child device for immediate unlock/unlink.
    /// </summary>
    Task<ServiceResult<DeviceModeRequestResponseDto>> DirectParentUnlockAsync(int deviceId, int childId, DirectParentUnlockDto dto);
}
