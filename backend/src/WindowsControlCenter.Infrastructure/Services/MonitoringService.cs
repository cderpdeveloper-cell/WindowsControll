using Microsoft.EntityFrameworkCore;
using WindowsControlCenter.Application.DTOs;
using WindowsControlCenter.Application.Interfaces;
using WindowsControlCenter.Domain.Entities;
using WindowsControlCenter.Domain.Enums;
using WindowsControlCenter.Infrastructure.Data;

namespace WindowsControlCenter.Infrastructure.Services;

public class MonitoringService : IMonitoringService
{
    private readonly AppDbContext _db;

    public MonitoringService(AppDbContext db) => _db = db;

    public async Task StoreMetricsAsync(MetricsDataDto metrics)
    {
        var entity = new MonitoringMetric
        {
            ComputerId = metrics.ComputerId,
            Timestamp = metrics.Timestamp,
            CpuUsagePercent = metrics.CpuUsagePercent,
            MemoryUsagePercent = metrics.MemoryUsagePercent,
            MemoryUsedBytes = metrics.MemoryUsedBytes,
            MemoryTotalBytes = metrics.MemoryTotalBytes,
            DiskUsagePercent = metrics.DiskUsagePercent,
            DiskUsedBytes = metrics.DiskUsedBytes,
            DiskTotalBytes = metrics.DiskTotalBytes,
            NetworkSentBytesPerSec = metrics.NetworkSentBytesPerSec,
            NetworkReceivedBytesPerSec = metrics.NetworkReceivedBytesPerSec,
            CpuTemperature = metrics.CpuTemperature,
            GpuUsagePercent = metrics.GpuUsagePercent,
            GpuTemperature = metrics.GpuTemperature,
            UptimeSeconds = metrics.UptimeSeconds,
            BatteryPercent = metrics.BatteryPercent,
            IsCharging = metrics.IsCharging
        };

        _db.MonitoringMetrics.Add(entity);
        await _db.SaveChangesAsync();
    }

    public async Task<List<MetricsDataDto>> GetMetricsAsync(Guid computerId, DateTime from, DateTime to, int maxPoints = 100)
    {
        var query = _db.MonitoringMetrics
            .Where(m => m.ComputerId == computerId && m.Timestamp >= from && m.Timestamp <= to)
            .OrderBy(m => m.Timestamp);

        var totalCount = await query.CountAsync();

        // Downsample if too many points
        if (totalCount > maxPoints && maxPoints > 0)
        {
            var skip = Math.Max(1, totalCount / maxPoints);
            var all = await query.ToListAsync();
            var sampledList = all.Where((_, idx) => idx % skip == 0).ToList();
            return sampledList.Select(MapToDto).ToList();
        }

        var items = await query.ToListAsync();
        return items.Select(MapToDto).ToList();
    }

    public async Task<MetricsDataDto?> GetLatestMetricsAsync(Guid computerId)
    {
        var metric = await _db.MonitoringMetrics
            .Where(m => m.ComputerId == computerId)
            .OrderByDescending(m => m.Timestamp)
            .FirstOrDefaultAsync();

        return metric == null ? null : MapToDto(metric);
    }

    private static MetricsDataDto MapToDto(MonitoringMetric m) => new(
        m.ComputerId, m.Timestamp,
        m.CpuUsagePercent, m.MemoryUsagePercent, m.MemoryUsedBytes, m.MemoryTotalBytes,
        m.DiskUsagePercent, m.DiskUsedBytes, m.DiskTotalBytes,
        m.NetworkSentBytesPerSec, m.NetworkReceivedBytesPerSec,
        m.CpuTemperature, m.GpuUsagePercent, m.GpuTemperature,
        m.UptimeSeconds, m.BatteryPercent, m.IsCharging);
}

public class AlertService : IAlertService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public AlertService(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<PagedResult<AlertDto>> GetAlertsAsync(Guid organizationId, int page = 1, int pageSize = 20,
        string? severity = null, string? status = null)
    {
        var query = _db.Alerts
            .Include(a => a.Computer)
            .Where(a => a.Computer.OrganizationId == organizationId)
            .AsQueryable();

        if (!string.IsNullOrEmpty(severity) && Enum.TryParse<AlertSeverity>(severity, true, out var sev))
            query = query.Where(a => a.Severity == sev);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<AlertStatus>(status, true, out var stat))
            query = query.Where(a => a.Status == stat);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AlertDto(a.Id, a.ComputerId, a.Computer.ComputerName, a.Title, a.Message,
                a.Severity, a.Status, a.Category, a.CreatedAt, a.AcknowledgedAt, a.AcknowledgedBy,
                a.ResolvedAt, a.ResolvedBy))
            .ToListAsync();

        return new PagedResult<AlertDto>(items, totalCount, page, pageSize, (int)Math.Ceiling((double)totalCount / pageSize));
    }

    public async Task<AlertDto> CreateAlertAsync(Guid computerId, string title, string? message, AlertSeverity severity, string? category = null)
    {
        var alert = new Alert
        {
            ComputerId = computerId,
            Title = title,
            Message = message,
            Severity = severity,
            Category = category,
            Status = AlertStatus.Active
        };

        _db.Alerts.Add(alert);
        await _db.SaveChangesAsync();

        var computer = await _db.Computers.FindAsync(computerId);
        return new AlertDto(alert.Id, alert.ComputerId, computer?.ComputerName, alert.Title, alert.Message,
            alert.Severity, alert.Status, alert.Category, alert.CreatedAt, null, null, null, null);
    }

    public async Task AcknowledgeAlertAsync(Guid alertId, string userId)
    {
        var alert = await _db.Alerts.FindAsync(alertId)
            ?? throw new KeyNotFoundException("Alert not found.");

        alert.Status = AlertStatus.Acknowledged;
        alert.AcknowledgedAt = DateTime.UtcNow;
        alert.AcknowledgedBy = userId;
        await _db.SaveChangesAsync();
    }

    public async Task ResolveAlertAsync(Guid alertId, string userId)
    {
        var alert = await _db.Alerts.FindAsync(alertId)
            ?? throw new KeyNotFoundException("Alert not found.");

        alert.Status = AlertStatus.Resolved;
        alert.ResolvedAt = DateTime.UtcNow;
        alert.ResolvedBy = userId;
        await _db.SaveChangesAsync();
    }

    public async Task EvaluateAlertsAsync(Guid computerId, MetricsDataDto metrics)
    {
        var computer = await _db.Computers.FindAsync(computerId);
        if (computer == null) return;

        var rules = await _db.AlertRules
            .Where(r => r.IsEnabled && r.OrganizationId == computer.OrganizationId)
            .ToListAsync();

        foreach (var rule in rules)
        {
            var value = GetMetricValue(metrics, rule.Metric);
            if (value == null) continue;

            var violated = rule.Operator switch
            {
                ">" => value > rule.Threshold,
                ">=" => value >= rule.Threshold,
                "<" => value < rule.Threshold,
                "<=" => value <= rule.Threshold,
                "==" => Math.Abs(value.Value - rule.Threshold) < 0.001,
                _ => false
            };

            if (violated)
            {
                // Check if there's already an active alert for this rule
                var existingAlert = await _db.Alerts
                    .FirstOrDefaultAsync(a => a.ComputerId == computerId &&
                                              a.Category == rule.Name &&
                                              a.Status == AlertStatus.Active);

                if (existingAlert == null)
                {
                    await CreateAlertAsync(computerId,
                        $"{rule.Name}: {rule.Metric} is {value:F1} (threshold: {rule.Operator} {rule.Threshold})",
                        $"Rule '{rule.Name}' triggered. Current value: {value:F1}",
                        rule.Severity, rule.Name);
                }
            }
        }
    }

    public async Task<List<AlertRuleDto>> GetAlertRulesAsync(Guid organizationId)
    {
        return await _db.AlertRules
            .Where(r => r.OrganizationId == organizationId)
            .Select(r => new AlertRuleDto(r.Id, r.Name, r.Description, r.Metric, r.Operator,
                r.Threshold, r.DurationSeconds, r.Severity, r.IsEnabled))
            .ToListAsync();
    }

    public async Task<AlertRuleDto> CreateAlertRuleAsync(CreateAlertRuleRequest request, Guid organizationId)
    {
        var rule = new AlertRule
        {
            Name = request.Name,
            Description = request.Description,
            Metric = request.Metric,
            Operator = request.Operator,
            Threshold = request.Threshold,
            DurationSeconds = request.DurationSeconds,
            Severity = request.Severity,
            IsEnabled = true,
            OrganizationId = organizationId
        };

        _db.AlertRules.Add(rule);
        await _db.SaveChangesAsync();

        return new AlertRuleDto(rule.Id, rule.Name, rule.Description, rule.Metric, rule.Operator,
            rule.Threshold, rule.DurationSeconds, rule.Severity, rule.IsEnabled);
    }

    private static double? GetMetricValue(MetricsDataDto metrics, string metricName) => metricName switch
    {
        "CpuUsagePercent" => metrics.CpuUsagePercent,
        "MemoryUsagePercent" => metrics.MemoryUsagePercent,
        "DiskUsagePercent" => metrics.DiskUsagePercent,
        "CpuTemperature" => metrics.CpuTemperature,
        "GpuUsagePercent" => metrics.GpuUsagePercent,
        "BatteryPercent" => metrics.BatteryPercent,
        _ => null
    };
}
