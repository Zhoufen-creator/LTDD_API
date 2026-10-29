using tran_nguyen_vi_khang_api.DTOs.Common;
using tran_nguyen_vi_khang_api.DTOs.Location;

namespace tran_nguyen_vi_khang_api.Services.Interfaces;

public interface ILocationService
{
    Task<ServiceResult<DeviceLocationResponseDto>> RecordLocationAsync(int deviceId, int childId, CreateLocationDto dto);
    Task<ServiceResult<DeviceLocationResponseDto?>> GetLatestLocationAsync(int childId, int parentId, bool isAdmin);
    Task<ServiceResult<IEnumerable<DeviceLocationResponseDto>>> GetLocationHistoryAsync(int childId, int parentId, bool isAdmin, int limit = 50);

    // Parent-Requested Location Refresh
    Task<ServiceResult<LocationRefreshRequestResponseDto>> CreateRefreshRequestAsync(int parentId, bool isAdmin, CreateLocationRefreshRequestDto dto);
    Task<ServiceResult<LocationRefreshRequestResponseDto>> GetRefreshRequestStatusAsync(int parentId, bool isAdmin, int requestId);
    Task<ServiceResult<LocationRefreshRequestResponseDto?>> GetPendingRefreshRequestForDeviceAsync(int deviceId);
    Task<ServiceResult<LocationRefreshRequestResponseDto>> CompleteRefreshRequestAsync(int deviceId, int requestId, CompleteRefreshRequestDto dto);
}
