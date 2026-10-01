using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using WindowsControlCenter.Application.DTOs;
using WindowsControlCenter.Application.Interfaces;
using WindowsControlCenter.Domain.Enums;

namespace WindowsControlCenter.API.Hubs;

/// <summary>
/// SignalR hub for agent communication (heartbeat, metrics, commands).
/// </summary>
public class AgentHub : Hub
{
    private readonly IAgentService _agentService;
    private readonly IMonitoringService _monitoringService;
    private readonly IAlertService _alertService;
    private readonly IComputerService _computerService;
    private readonly ILogger<AgentHub> _logger;

    public AgentHub(IAgentService agentService, IMonitoringService monitoringService,
        IAlertService alertService, IComputerService computerService, ILogger<AgentHub> logger)
    {
        _agentService = agentService;
        _monitoringService = monitoringService;
        _alertService = alertService;
        _computerService = computerService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Agent connection established: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Try to find agent by connection ID and mark offline
        var agentIdStr = Context.GetHttpContext()?.Request.Query["agentId"].FirstOrDefault();
        if (Guid.TryParse(agentIdStr, out var agentId))
        {
            await _agentService.UpdateAgentConnectionAsync(agentId, null, AgentStatus.Disconnected);
            _logger.LogInformation("Agent {AgentId} disconnected", agentId);

            // Notify dashboard clients
            await Clients.Group("dashboard").SendAsync("AgentOffline", agentId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Agent authenticates after connecting.
    /// </summary>
    public async Task<bool> AuthenticateAgent(Guid agentId, string apiToken)
    {
        var isValid = await _agentService.ValidateAgentTokenAsync(agentId, apiToken);
        if (!isValid)
        {
            _logger.LogWarning("Agent authentication failed for {AgentId}", agentId);
            return false;
        }

        await _agentService.UpdateAgentConnectionAsync(agentId, Context.ConnectionId, AgentStatus.Connected);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"agent-{agentId}");

        // Notify dashboard clients
        await Clients.Group("dashboard").SendAsync("AgentOnline", agentId);

        _logger.LogInformation("Agent {AgentId} authenticated successfully", agentId);
        return true;
    }

    /// <summary>
    /// Agent sends heartbeat.
    /// </summary>
    public async Task Heartbeat(AgentHeartbeatRequest request)
    {
        await _agentService.ProcessHeartbeatAsync(request);
        await Clients.Group("dashboard").SendAsync("Heartbeat", request.AgentId);
    }

    /// <summary>
    /// Agent sends real-time metrics.
    /// </summary>
    public async Task SendMetrics(MetricsDataDto metrics)
    {
        await _monitoringService.StoreMetricsAsync(metrics);
        await _alertService.EvaluateAlertsAsync(metrics.ComputerId, metrics);

        // Forward to dashboard clients watching this computer
        await Clients.Group("dashboard").SendAsync("MetricsUpdated", metrics);
        await Clients.Group($"computer-{metrics.ComputerId}").SendAsync("MetricsUpdated", metrics);
    }

    /// <summary>
    /// Agent reports system information update.
    /// </summary>
    public async Task UpdateSystemInfo(Guid computerId, ComputerSystemInfoDto info)
    {
        // This would update the computer's hardware/software info in the database
        await Clients.Group("dashboard").SendAsync("SystemInfoUpdated", computerId, info);
    }

    /// <summary>
    /// Agent reports command execution result.
    /// </summary>
    public async Task CommandResult(Guid computerId, string commandId, string result, bool success)
    {
        await Clients.Group("dashboard").SendAsync("CommandCompleted", computerId, commandId, result, success);
        await Clients.Group($"computer-{computerId}").SendAsync("CommandCompleted", commandId, result, success);
    }

    /// <summary>
    /// Agent sends process list.
    /// </summary>
    public async Task SendProcessList(Guid computerId, List<ProcessDto> processes)
    {
        await Clients.Group($"computer-{computerId}").SendAsync("ProcessListUpdated", processes);
    }

    /// <summary>
    /// Agent sends service list.
    /// </summary>
    public async Task SendServiceList(Guid computerId, List<WindowsServiceDto> services)
    {
        await Clients.Group($"computer-{computerId}").SendAsync("ServiceListUpdated", services);
    }

    /// <summary>
    /// Agent sends file listing.
    /// </summary>
    public async Task SendFileList(Guid computerId, string path, List<FileEntryDto> files)
    {
        await Clients.Group($"computer-{computerId}").SendAsync("FileListUpdated", path, files);
    }

    /// <summary>
    /// Agent sends drive listing.
    /// </summary>
    public async Task SendDriveList(Guid computerId, List<DriveEntryDto> drives)
    {
        await Clients.Group($"computer-{computerId}").SendAsync("DriveListUpdated", drives);
    }
}

/// <summary>
/// SignalR hub for dashboard real-time updates.
/// </summary>
[Authorize]
public class DashboardHub : Hub
{
    private readonly ILogger<DashboardHub> _logger;

    public DashboardHub(ILogger<DashboardHub> logger) => _logger = logger;

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "dashboard");
        _logger.LogInformation("Dashboard client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Subscribe to a specific computer's updates.
    /// </summary>
    public async Task WatchComputer(Guid computerId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"computer-{computerId}");
        _logger.LogInformation("Client {ConnectionId} watching computer {ComputerId}", Context.ConnectionId, computerId);
    }

    /// <summary>
    /// Unsubscribe from a computer's updates.
    /// </summary>
    public async Task UnwatchComputer(Guid computerId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"computer-{computerId}");
    }
}

/// <summary>
/// DTO for system info updates from the agent.
/// </summary>
public record ComputerSystemInfoDto(
    string? OsName, string? OsVersion, string? OsBuild, string? OsEdition,
    string? CpuName, int? CpuCores, int? CpuThreads, long? TotalMemoryBytes,
    string? GpuName, string? Motherboard, string? BiosVersion, string? SerialNumber,
    string? IpAddress, string? MacAddress, string? PublicIpAddress);
