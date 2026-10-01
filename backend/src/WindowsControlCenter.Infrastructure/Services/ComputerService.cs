using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WindowsControlCenter.Application.DTOs;
using WindowsControlCenter.Application.Interfaces;
using WindowsControlCenter.Domain.Entities;
using WindowsControlCenter.Domain.Enums;
using WindowsControlCenter.Infrastructure.Data;

namespace WindowsControlCenter.Infrastructure.Services;

public class ComputerService : IComputerService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ILogger<ComputerService> _logger;

    public ComputerService(AppDbContext db, IAuditService audit, ILogger<ComputerService> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public async Task<PagedResult<ComputerSummaryDto>> GetComputersAsync(Guid organizationId, int page = 1, int pageSize = 20,
        string? search = null, string? status = null, string? sortBy = null, bool sortDesc = false)
    {
        var query = _db.Computers
            .Include(c => c.Agent)
            .Where(c => c.OrganizationId == organizationId)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(c => c.ComputerName.Contains(search) ||
                                     (c.IpAddress != null && c.IpAddress.Contains(search)) ||
                                     (c.OsName != null && c.OsName.Contains(search)));

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<ComputerStatus>(status, true, out var cs))
            query = query.Where(c => c.Status == cs);

        // Sorting
        query = sortBy?.ToLower() switch
        {
            "name" => sortDesc ? query.OrderByDescending(c => c.ComputerName) : query.OrderBy(c => c.ComputerName),
            "status" => sortDesc ? query.OrderByDescending(c => c.IsOnline) : query.OrderBy(c => c.IsOnline),
            "lastseen" => sortDesc ? query.OrderByDescending(c => c.LastSeenAt) : query.OrderBy(c => c.LastSeenAt),
            "ip" => sortDesc ? query.OrderByDescending(c => c.IpAddress) : query.OrderBy(c => c.IpAddress),
            _ => query.OrderByDescending(c => c.IsOnline).ThenBy(c => c.ComputerName)
        };

        var totalCount = await query.CountAsync();

        // Get latest metric for each computer in results
        var computers = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var computerIds = computers.Select(c => c.Id).ToList();
        var latestMetrics = await _db.MonitoringMetrics
            .Where(m => computerIds.Contains(m.ComputerId))
            .GroupBy(m => m.ComputerId)
            .Select(g => g.OrderByDescending(m => m.Timestamp).First())
            .ToDictionaryAsync(m => m.ComputerId);

        var items = computers.Select(c =>
        {
            latestMetrics.TryGetValue(c.Id, out var metric);
            return new ComputerSummaryDto(
                c.Id, c.ComputerName, c.IpAddress, c.OsName,
                c.Status, c.IsOnline, c.LastSeenAt,
                c.Agent?.AgentVersion,
                metric?.CpuUsagePercent, metric?.MemoryUsagePercent, metric?.DiskUsagePercent);
        }).ToList();

        return new PagedResult<ComputerSummaryDto>(items, totalCount, page, pageSize, (int)Math.Ceiling((double)totalCount / pageSize));
    }

    public async Task<ComputerDto?> GetComputerByIdAsync(Guid id, Guid organizationId)
    {
        var c = await _db.Computers
            .Include(c => c.Agent)
            .Include(c => c.ComputerTags).ThenInclude(ct => ct.Tag)
            .FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == organizationId);

        if (c == null) return null;

        return MapToDto(c);
    }

    public async Task<ComputerDto> CreateComputerAsync(CreateComputerRequest request, Guid organizationId, string userId)
    {
        var computer = new Computer
        {
            ComputerName = request.ComputerName,
            Hostname = request.Hostname,
            IpAddress = request.IpAddress,
            MacAddress = request.MacAddress,
            Notes = request.Notes,
            OrganizationId = organizationId,
            Status = ComputerStatus.Registered,
            CreatedBy = userId
        };

        _db.Computers.Add(computer);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Computer.Create", "Computer", $"Created computer {computer.ComputerName}",
            organizationId: organizationId, computerId: computer.Id, result: "Success");

        return MapToDto(computer);
    }

    public async Task<ComputerDto> UpdateComputerAsync(Guid id, UpdateComputerRequest request, Guid organizationId, string userId)
    {
        var computer = await _db.Computers
            .Include(c => c.Agent)
            .Include(c => c.ComputerTags).ThenInclude(ct => ct.Tag)
            .FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == organizationId)
            ?? throw new KeyNotFoundException("Computer not found.");

        if (request.Owner != null) computer.Owner = request.Owner;
        if (request.Department != null) computer.Department = request.Department;
        if (request.Location != null) computer.Location = request.Location;
        if (request.AssetId != null) computer.AssetId = request.AssetId;
        if (request.Notes != null) computer.Notes = request.Notes;
        computer.UpdatedBy = userId;

        await _db.SaveChangesAsync();

        await _audit.LogAsync("Computer.Update", "Computer", $"Updated computer {computer.ComputerName}",
            organizationId: organizationId, computerId: computer.Id, result: "Success");

        return MapToDto(computer);
    }

    public async Task DeleteComputerAsync(Guid id, Guid organizationId)
    {
        var computer = await _db.Computers
            .FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == organizationId)
            ?? throw new KeyNotFoundException("Computer not found.");

        computer.IsDeleted = true;
        computer.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Computer.Delete", "Computer", $"Archived computer {computer.ComputerName}",
            organizationId: organizationId, computerId: computer.Id, result: "Success");
    }

    public async Task UpdateComputerStatusAsync(Guid computerId, bool isOnline)
    {
        var computer = await _db.Computers.FindAsync(computerId);
        if (computer == null) return;

        computer.IsOnline = isOnline;
        computer.LastSeenAt = DateTime.UtcNow;
        if (isOnline) computer.Status = ComputerStatus.Online;
        else if (computer.Status == ComputerStatus.Online) computer.Status = ComputerStatus.Offline;

        await _db.SaveChangesAsync();
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(Guid organizationId)
    {
        var computers = await _db.Computers
            .Where(c => c.OrganizationId == organizationId)
            .ToListAsync();

        var total = computers.Count;
        var online = computers.Count(c => c.IsOnline);
        var offline = total - online;

        var alerts = await _db.Alerts
            .Where(a => a.Computer.OrganizationId == organizationId && a.Status == AlertStatus.Active)
            .ToListAsync();

        var warningAlerts = alerts.Count(a => a.Severity == AlertSeverity.Warning);
        var criticalAlerts = alerts.Count(a => a.Severity == AlertSeverity.Critical);

        // Get latest metrics for online computers
        var onlineIds = computers.Where(c => c.IsOnline).Select(c => c.Id).ToList();
        var latestMetrics = new List<MonitoringMetric>();
        if (onlineIds.Count > 0)
        {
            latestMetrics = await _db.MonitoringMetrics
                .Where(m => onlineIds.Contains(m.ComputerId))
                .GroupBy(m => m.ComputerId)
                .Select(g => g.OrderByDescending(m => m.Timestamp).First())
                .ToListAsync();
        }

        var avgCpu = latestMetrics.Count > 0 ? latestMetrics.Average(m => m.CpuUsagePercent ?? 0) : (double?)null;
        var avgMem = latestMetrics.Count > 0 ? latestMetrics.Average(m => m.MemoryUsagePercent ?? 0) : (double?)null;
        var avgDisk = latestMetrics.Count > 0 ? latestMetrics.Average(m => m.DiskUsagePercent ?? 0) : (double?)null;

        var recentAlerts = await _db.Alerts
            .Include(a => a.Computer)
            .Where(a => a.Computer.OrganizationId == organizationId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .Select(a => new RecentAlertDto(a.Id, a.Title, a.Severity, a.Status, a.Computer.ComputerName, a.CreatedAt))
            .ToListAsync();

        var recentComputers = computers
            .OrderByDescending(c => c.IsOnline)
            .ThenByDescending(c => c.LastSeenAt)
            .Take(10)
            .Select(c =>
            {
                var metric = latestMetrics.FirstOrDefault(m => m.ComputerId == c.Id);
                return new ComputerSummaryDto(c.Id, c.ComputerName, c.IpAddress, c.OsName,
                    c.Status, c.IsOnline, c.LastSeenAt, c.Agent?.AgentVersion,
                    metric?.CpuUsagePercent, metric?.MemoryUsagePercent, metric?.DiskUsagePercent);
            }).ToList();

        return new DashboardStatsDto(total, online, offline, warningAlerts, criticalAlerts,
            avgCpu, avgMem, avgDisk, recentAlerts, recentComputers);
    }

    private static ComputerDto MapToDto(Computer c) => new(
        c.Id, c.ComputerName, c.Hostname, c.Domain, c.MachineId,
        c.OsName, c.OsVersion, c.OsBuild, c.OsEdition, c.OsArchitecture,
        c.CpuName, c.CpuCores, c.CpuThreads, c.TotalMemoryBytes,
        c.GpuName, c.Motherboard, c.BiosVersion, c.SerialNumber,
        c.IpAddress, c.MacAddress, c.PublicIpAddress,
        c.Status, c.LastSeenAt, c.IsOnline,
        c.Owner, c.Department, c.Location, c.AssetId, c.Notes,
        c.Agent != null ? new AgentSummaryDto(c.Agent.AgentId, c.Agent.AgentVersion, c.Agent.Status, c.Agent.LastHeartbeatAt, c.Agent.LastKnownIp) : null,
        c.ComputerTags?.Select(ct => ct.Tag.Name).ToList(),
        c.OrganizationId);
}
