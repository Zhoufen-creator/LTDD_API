using tran_nguyen_vi_khang_api.DTOs.Child;
using tran_nguyen_vi_khang_api.DTOs.Common;

namespace tran_nguyen_vi_khang_api.Services.Interfaces;

public interface IChildService
{
    Task<IEnumerable<ChildResponseDto>> GetAllAsync(int currentUserId, bool isAdmin = false, int? parentId = null);
    Task<ChildResponseDto?> GetByIdAsync(int id, int currentUserId, bool isAdmin = false);
    Task<ServiceResult<ChildResponseDto>> CreateAsync(CreateChildDto dto, int currentUserId);
    Task<ServiceResult<ChildResponseDto>> UpdateAsync(int id, UpdateChildDto dto, int currentUserId, bool isAdmin = false);
    Task<ServiceResult<bool>> DeleteAsync(int id, int currentUserId, bool isAdmin = false);
}
