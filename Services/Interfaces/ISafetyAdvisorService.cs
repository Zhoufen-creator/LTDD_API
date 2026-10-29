using tran_nguyen_vi_khang_api.DTOs.Common;
using tran_nguyen_vi_khang_api.DTOs.SafetyAdvisor;

namespace tran_nguyen_vi_khang_api.Services.Interfaces;

/// <summary>
/// Service interface for evaluating child safety telemetry and generating actionable advisor warnings.
/// </summary>
public interface ISafetyAdvisorService
{
    /// <summary>
    /// Analyzes stored battery, device, and location data for a specific child to provide safety recommendations.
    /// Strictly enforces parent ownership (or admin permission).
    /// </summary>
    /// <param name="childId">The target child ID.</param>
    /// <param name="currentUserId">Authenticated caller user ID.</param>
    /// <param name="isAdmin">Whether caller has Admin privileges.</param>
    /// <param name="evaluationTimeUtc">Optional explicit UTC timestamp for deterministic testing.</param>
    /// <returns>Structured safety evaluation and warnings.</returns>
    Task<ServiceResult<SafetyAdvisorResponseDto>> GetSafetyAdvisorAsync(
        int childId,
        int currentUserId,
        bool isAdmin,
        DateTime? evaluationTimeUtc = null);
}

