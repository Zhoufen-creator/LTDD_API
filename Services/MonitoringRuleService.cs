using Microsoft.EntityFrameworkCore;
using tran_nguyen_vi_khang_api.Data;
using tran_nguyen_vi_khang_api.DTOs.Common;
using tran_nguyen_vi_khang_api.DTOs.MonitoringRule;
using tran_nguyen_vi_khang_api.Models;
using tran_nguyen_vi_khang_api.Services.Interfaces;

namespace tran_nguyen_vi_khang_api.Services;

public class MonitoringRuleService : IMonitoringRuleService
{
    private readonly AppDbContext _context;

    public MonitoringRuleService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves monitoring rules for children belonging to the authenticated parent.
    /// </summary>
    public async Task<IEnumerable<MonitoringRuleResponseDto>> GetAllAsync(int currentUserId, bool isAdmin = false, int? childId = null)
    {
        var query = _context.MonitoringRules.AsNoTracking().AsQueryable();

        if (!isAdmin)
        {
            query = query.Where(r => r.Child.ParentId == currentUserId);
        }

        if (childId.HasValue)
        {
            query = query.Where(r => r.ChildId == childId.Value);
        }

        return await query
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
    }

    /// <summary>
    /// Retrieves a monitoring rule by its ID with ownership verification.
    /// </summary>
    public async Task<MonitoringRuleResponseDto?> GetByIdAsync(int id, int currentUserId, bool isAdmin = false)
    {
        var query = _context.MonitoringRules
            .AsNoTracking()
            .Where(r => r.Id == id);

        if (!isAdmin)
        {
            query = query.Where(r => r.Child.ParentId == currentUserId);
        }

        return await query
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
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Creates a new monitoring rule for a child.
    /// Verifies that the child belongs to the authenticated parent.
    /// </summary>
    public async Task<ServiceResult<MonitoringRuleResponseDto>> CreateAsync(CreateMonitoringRuleDto dto, int currentUserId, bool isAdmin = false)
    {
        var trimmedName = dto.RuleName?.Trim();
        var trimmedType = dto.RuleType?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return ServiceResult<MonitoringRuleResponseDto>.BadRequest("RuleName cannot be empty or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(trimmedType))
        {
            return ServiceResult<MonitoringRuleResponseDto>.BadRequest("RuleType cannot be empty or whitespace.");
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
            return ServiceResult<MonitoringRuleResponseDto>.BadRequest($"Child with ID {dto.ChildId} does not exist or does not belong to your account.");
        }

        var rule = new MonitoringRule
        {
            RuleName = trimmedName,
            RuleType = trimmedType,
            Description = dto.Description?.Trim(),
            RuleConfig = dto.RuleConfig,
            IsEnabled = dto.IsEnabled,
            Version = 1,
            SyncStatus = "ServerSaved",
            LastModifiedAt = DateTime.UtcNow,
            ChildId = dto.ChildId,
            CreatedAt = DateTime.UtcNow
        };

        _context.MonitoringRules.Add(rule);
        await _context.SaveChangesAsync();

        var response = new MonitoringRuleResponseDto
        {
            Id = rule.Id,
            RuleName = rule.RuleName,
            RuleType = rule.RuleType,
            Description = rule.Description,
            IsEnabled = rule.IsEnabled,
            RuleConfig = rule.RuleConfig,
            Version = rule.Version,
            SyncStatus = rule.SyncStatus,
            LastModifiedAt = rule.LastModifiedAt,
            ChildId = rule.ChildId,
            CreatedAt = rule.CreatedAt
        };

        return ServiceResult<MonitoringRuleResponseDto>.Success(response);
    }

    /// <summary>
    /// Updates an existing monitoring rule with ownership verification.
    /// </summary>
    public async Task<ServiceResult<MonitoringRuleResponseDto>> UpdateAsync(int id, UpdateMonitoringRuleDto dto, int currentUserId, bool isAdmin = false)
    {
        var query = _context.MonitoringRules.Include(r => r.Child).Where(r => r.Id == id);
        if (!isAdmin)
        {
            query = query.Where(r => r.Child.ParentId == currentUserId);
        }

        var rule = await query.FirstOrDefaultAsync();
        if (rule == null)
        {
            return ServiceResult<MonitoringRuleResponseDto>.NotFound($"MonitoringRule with ID {id} was not found.");
        }

        var trimmedName = dto.RuleName?.Trim();
        var trimmedType = dto.RuleType?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return ServiceResult<MonitoringRuleResponseDto>.BadRequest("RuleName cannot be empty or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(trimmedType))
        {
            return ServiceResult<MonitoringRuleResponseDto>.BadRequest("RuleType cannot be empty or whitespace.");
        }

        // If reassigning to another child, verify target child belongs to current user
        if (dto.ChildId.HasValue && dto.ChildId.Value != rule.ChildId)
        {
            var targetChildQuery = _context.Children.Where(c => c.Id == dto.ChildId.Value);
            if (!isAdmin)
            {
                targetChildQuery = targetChildQuery.Where(c => c.ParentId == currentUserId);
            }

            var targetChildExists = await targetChildQuery.AnyAsync();
            if (!targetChildExists)
            {
                return ServiceResult<MonitoringRuleResponseDto>.BadRequest($"Target child with ID {dto.ChildId.Value} does not exist or does not belong to your account.");
            }
            rule.ChildId = dto.ChildId.Value;
        }

        rule.RuleName = trimmedName;
        rule.RuleType = trimmedType;
        rule.Description = dto.Description?.Trim();
        rule.RuleConfig = dto.RuleConfig;
        rule.IsEnabled = dto.IsEnabled;
        rule.Version++;
        if (!string.IsNullOrWhiteSpace(dto.SyncStatus))
        {
            rule.SyncStatus = dto.SyncStatus.Trim();
        }
        rule.LastModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var response = new MonitoringRuleResponseDto
        {
            Id = rule.Id,
            RuleName = rule.RuleName,
            RuleType = rule.RuleType,
            Description = rule.Description,
            IsEnabled = rule.IsEnabled,
            RuleConfig = rule.RuleConfig,
            Version = rule.Version,
            SyncStatus = rule.SyncStatus,
            LastModifiedAt = rule.LastModifiedAt,
            ChildId = rule.ChildId,
            CreatedAt = rule.CreatedAt
        };

        return ServiceResult<MonitoringRuleResponseDto>.Success(response);
    }

    /// <summary>
    /// Deletes a monitoring rule with ownership verification.
    /// </summary>
    public async Task<ServiceResult<bool>> DeleteAsync(int id, int currentUserId, bool isAdmin = false)
    {
        var query = _context.MonitoringRules.Include(r => r.Child).Where(r => r.Id == id);
        if (!isAdmin)
        {
            query = query.Where(r => r.Child.ParentId == currentUserId);
        }

        var rule = await query.FirstOrDefaultAsync();
        if (rule == null)
        {
            return ServiceResult<bool>.NotFound($"MonitoringRule with ID {id} was not found.");
        }

        _context.MonitoringRules.Remove(rule);
        await _context.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }
}
