using tran_nguyen_vi_khang_api.DTOs.Common;
using tran_nguyen_vi_khang_api.DTOs.MonitoringRule;

namespace tran_nguyen_vi_khang_api.Services.Interfaces;

public interface IMonitoringRuleService
{
    Task<IEnumerable<MonitoringRuleResponseDto>> GetAllAsync(int currentUserId, bool isAdmin = false, int? childId = null);
    Task<MonitoringRuleResponseDto?> GetByIdAsync(int id, int currentUserId, bool isAdmin = false);
    Task<ServiceResult<MonitoringRuleResponseDto>> CreateAsync(CreateMonitoringRuleDto dto, int currentUserId, bool isAdmin = false);
    Task<ServiceResult<MonitoringRuleResponseDto>> UpdateAsync(int id, UpdateMonitoringRuleDto dto, int currentUserId, bool isAdmin = false);
    Task<ServiceResult<bool>> DeleteAsync(int id, int currentUserId, bool isAdmin = false);
}
