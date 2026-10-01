using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WindowsControlCenter.Domain.Enums;
using WindowsControlCenter.Infrastructure.Data;

namespace WindowsControlCenter.API.BackgroundServices;

/// <summary>
/// Periodically checks for agents that haven't sent a heartbeat and marks them offline.
/// </summary>
public class OfflineDetectionService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OfflineDetectionService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _offlineThreshold = TimeSpan.FromMinutes(2);

    public OfflineDetectionService(IServiceProvider serviceProvider, ILogger<OfflineDetectionService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Offline detection service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var threshold = DateTime.UtcNow.Subtract(_offlineThreshold);

                // Find agents that were connected but haven't sent heartbeat
                var staleAgents = await db.Agents
                    .Include(a => a.Computer)
                    .Where(a => a.Status == AgentStatus.Connected &&
                                (a.LastHeartbeatAt == null || a.LastHeartbeatAt < threshold))
                    .ToListAsync(stoppingToken);

                foreach (var agent in staleAgents)
                {
                    agent.Status = AgentStatus.Disconnected;
                    agent.ConnectionId = null;
                    agent.Computer.IsOnline = false;
                    agent.Computer.Status = ComputerStatus.Offline;

                    _logger.LogInformation("Marked agent {AgentId} ({ComputerName}) as offline — no heartbeat since {LastHeartbeat}",
                        agent.AgentId, agent.Computer.ComputerName, agent.LastHeartbeatAt);
                }

                if (staleAgents.Count > 0)
                    await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in offline detection");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }
}

/// <summary>
/// Periodically cleans up expired enrollment codes and old metrics.
/// </summary>
public class CleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CleanupService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

    public CleanupService(IServiceProvider serviceProvider, ILogger<CleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Expire enrollment codes
                var expiredCodes = await db.EnrollmentCodes
                    .Where(ec => ec.Status == EnrollmentCodeStatus.Active && ec.ExpiresAt < DateTime.UtcNow)
                    .ToListAsync(stoppingToken);

                foreach (var code in expiredCodes)
                    code.Status = EnrollmentCodeStatus.Expired;

                // Clean old metrics (older than 90 days)
                var metricsThreshold = DateTime.UtcNow.AddDays(-90);
                var oldMetrics = await db.MonitoringMetrics
                    .Where(m => m.Timestamp < metricsThreshold)
                    .Take(10000)
                    .ToListAsync(stoppingToken);

                if (oldMetrics.Count > 0)
                {
                    db.MonitoringMetrics.RemoveRange(oldMetrics);
                    _logger.LogInformation("Cleaned {Count} old metrics", oldMetrics.Count);
                }

                // Clean old revoked refresh tokens
                var tokenThreshold = DateTime.UtcNow.AddDays(-30);
                var oldTokens = await db.RefreshTokens
                    .Where(t => t.RevokedAt != null && t.RevokedAt < tokenThreshold)
                    .Take(1000)
                    .ToListAsync(stoppingToken);

                if (oldTokens.Count > 0)
                    db.RefreshTokens.RemoveRange(oldTokens);

                await db.SaveChangesAsync(stoppingToken);

                if (expiredCodes.Count > 0 || oldMetrics.Count > 0 || oldTokens.Count > 0)
                {
                    _logger.LogInformation("Cleanup: {ExpiredCodes} codes expired, {OldMetrics} metrics cleaned, {OldTokens} tokens cleaned",
                        expiredCodes.Count, oldMetrics.Count, oldTokens.Count);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in cleanup service");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }
}
