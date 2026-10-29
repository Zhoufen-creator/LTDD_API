using Microsoft.EntityFrameworkCore;
using tran_nguyen_vi_khang_api.Data;
using tran_nguyen_vi_khang_api.DTOs.Child;
using tran_nguyen_vi_khang_api.DTOs.Common;
using tran_nguyen_vi_khang_api.Models;
using tran_nguyen_vi_khang_api.Services.Interfaces;

namespace tran_nguyen_vi_khang_api.Services;

public class ChildService : IChildService
{
    private readonly AppDbContext _context;

    public ChildService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves all children owned by the authenticated parent.
    /// Admins can retrieve all children or filter by parentId.
    /// </summary>
    public async Task<IEnumerable<ChildResponseDto>> GetAllAsync(int currentUserId, bool isAdmin = false, int? parentId = null)
    {
        var query = _context.Children.AsNoTracking().AsQueryable();

        if (!isAdmin)
        {
            // Strict ownership isolation: Parents can ONLY query their own children
            query = query.Where(c => c.ParentId == currentUserId);
        }
        else if (parentId.HasValue)
        {
            query = query.Where(c => c.ParentId == parentId.Value);
        }

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ChildResponseDto
            {
                Id = c.Id,
                FullName = c.FullName,
                DateOfBirth = c.DateOfBirth,
                ParentId = c.ParentId,
                CreatedAt = c.CreatedAt,
                DeviceCount = c.Devices.Count(),
                MonitoringRuleCount = c.MonitoringRules.Count()
            })
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves a child profile by ID with strict ownership verification.
    /// </summary>
    public async Task<ChildResponseDto?> GetByIdAsync(int id, int currentUserId, bool isAdmin = false)
    {
        var query = _context.Children
            .AsNoTracking()
            .Where(c => c.Id == id);

        if (!isAdmin)
        {
            query = query.Where(c => c.ParentId == currentUserId);
        }

        return await query
            .Select(c => new ChildResponseDto
            {
                Id = c.Id,
                FullName = c.FullName,
                DateOfBirth = c.DateOfBirth,
                ParentId = c.ParentId,
                CreatedAt = c.CreatedAt,
                DeviceCount = c.Devices.Count(),
                MonitoringRuleCount = c.MonitoringRules.Count()
            })
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Creates a new child profile.
    /// SECURITY: Forces ParentId from authenticated identity (currentUserId), ignoring client-supplied parentId.
    /// </summary>
    public async Task<ServiceResult<ChildResponseDto>> CreateAsync(CreateChildDto dto, int currentUserId)
    {
        var trimmedName = dto.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return ServiceResult<ChildResponseDto>.BadRequest("Child FullName cannot be empty or whitespace.");
        }

        if (dto.DateOfBirth.HasValue && dto.DateOfBirth.Value > DateTime.UtcNow)
        {
            return ServiceResult<ChildResponseDto>.BadRequest("Date of birth cannot be in the future.");
        }

        // Verify authenticated parent exists and is active in database
        var parent = await _context.Users.FindAsync(currentUserId);
        if (parent == null || !parent.IsActive)
        {
            return ServiceResult<ChildResponseDto>.BadRequest("Authenticated parent account does not exist or is inactive.");
        }

        var child = new Child
        {
            FullName = trimmedName,
            DateOfBirth = dto.DateOfBirth,
            ParentId = currentUserId, // Secure server-enforced ownership
            CreatedAt = DateTime.UtcNow
        };

        _context.Children.Add(child);
        await _context.SaveChangesAsync();

        var response = new ChildResponseDto
        {
            Id = child.Id,
            FullName = child.FullName,
            DateOfBirth = child.DateOfBirth,
            ParentId = child.ParentId,
            CreatedAt = child.CreatedAt,
            DeviceCount = 0,
            MonitoringRuleCount = 0
        };

        return ServiceResult<ChildResponseDto>.Success(response);
    }

    /// <summary>
    /// Updates an existing child profile with ownership check.
    /// </summary>
    public async Task<ServiceResult<ChildResponseDto>> UpdateAsync(int id, UpdateChildDto dto, int currentUserId, bool isAdmin = false)
    {
        var query = _context.Children
            .Include(c => c.Devices)
            .Include(c => c.MonitoringRules)
            .Where(c => c.Id == id);

        if (!isAdmin)
        {
            query = query.Where(c => c.ParentId == currentUserId);
        }

        var child = await query.FirstOrDefaultAsync();
        if (child == null)
        {
            return ServiceResult<ChildResponseDto>.NotFound($"Child with ID {id} was not found.");
        }

        var trimmedName = dto.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return ServiceResult<ChildResponseDto>.BadRequest("Child FullName cannot be empty or whitespace.");
        }

        if (dto.DateOfBirth.HasValue && dto.DateOfBirth.Value > DateTime.UtcNow)
        {
            return ServiceResult<ChildResponseDto>.BadRequest("Date of birth cannot be in the future.");
        }

        child.FullName = trimmedName;
        child.DateOfBirth = dto.DateOfBirth;

        await _context.SaveChangesAsync();

        var response = new ChildResponseDto
        {
            Id = child.Id,
            FullName = child.FullName,
            DateOfBirth = child.DateOfBirth,
            ParentId = child.ParentId,
            CreatedAt = child.CreatedAt,
            DeviceCount = child.Devices.Count,
            MonitoringRuleCount = child.MonitoringRules.Count
        };

        return ServiceResult<ChildResponseDto>.Success(response);
    }

    /// <summary>
    /// Deletes a child profile with ownership check.
    /// </summary>
    public async Task<ServiceResult<bool>> DeleteAsync(int id, int currentUserId, bool isAdmin = false)
    {
        var query = _context.Children.Where(c => c.Id == id);
        if (!isAdmin)
        {
            query = query.Where(c => c.ParentId == currentUserId);
        }

        var child = await query.FirstOrDefaultAsync();
        if (child == null)
        {
            return ServiceResult<bool>.NotFound($"Child with ID {id} was not found.");
        }

        _context.Children.Remove(child);
        await _context.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }
}
