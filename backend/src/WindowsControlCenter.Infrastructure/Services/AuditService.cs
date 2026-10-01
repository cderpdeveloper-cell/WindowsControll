using Microsoft.EntityFrameworkCore;
using WindowsControlCenter.Application.DTOs;
using WindowsControlCenter.Application.Interfaces;
using WindowsControlCenter.Domain.Entities;
using WindowsControlCenter.Infrastructure.Data;

namespace WindowsControlCenter.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;

    public AuditService(AppDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(string action, string? category = null, string? details = null,
        Guid? userId = null, string? username = null, Guid? computerId = null,
        string? ipAddress = null, string? result = null, Guid? organizationId = null)
    {
        var entry = new AuditLog
        {
            Action = action,
            Category = category,
            Details = details,
            UserId = userId,
            Username = username,
            ComputerId = computerId,
            IpAddress = ipAddress,
            Result = result,
            OrganizationId = organizationId ?? Guid.Empty,
            CorrelationId = Guid.NewGuid().ToString()
        };

        _db.AuditLogs.Add(entry);
        await _db.SaveChangesAsync();
    }

    public async Task<PagedResult<AuditLogDto>> GetLogsAsync(Guid organizationId, int page = 1, int pageSize = 50,
        string? category = null, string? search = null, DateTime? from = null, DateTime? to = null)
    {
        var query = _db.AuditLogs
            .Where(a => a.OrganizationId == organizationId)
            .AsQueryable();

        if (!string.IsNullOrEmpty(category))
            query = query.Where(a => a.Category == category);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(a => a.Action.Contains(search) || (a.Details != null && a.Details.Contains(search)));

        if (from.HasValue)
            query = query.Where(a => a.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(a => a.CreatedAt <= to.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogDto(
                a.Id, a.Username,
                a.Computer != null ? a.Computer.ComputerName : null,
                a.Action, a.Category, a.Details, a.IpAddress, a.Result, a.CreatedAt))
            .ToListAsync();

        return new PagedResult<AuditLogDto>(items, totalCount, page, pageSize, (int)Math.Ceiling((double)totalCount / pageSize));
    }
}
