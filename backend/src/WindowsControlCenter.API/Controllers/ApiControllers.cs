using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WindowsControlCenter.Application.DTOs;
using WindowsControlCenter.Application.Interfaces;
using WindowsControlCenter.API.Hubs;

namespace WindowsControlCenter.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ComputersController : ControllerBase
{
    private readonly IComputerService _computerService;
    private readonly IMonitoringService _monitoringService;
    private readonly IHubContext<AgentHub> _agentHub;

    public ComputersController(IComputerService computerService, IMonitoringService monitoringService,
        IHubContext<AgentHub> agentHub)
    {
        _computerService = computerService;
        _monitoringService = monitoringService;
        _agentHub = agentHub;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ComputerSummaryDto>>> GetComputers(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? status = null,
        [FromQuery] string? sortBy = null, [FromQuery] bool sortDesc = false)
    {
        var orgId = GetOrganizationId();
        var result = await _computerService.GetComputersAsync(orgId, page, pageSize, search, status, sortBy, sortDesc);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ComputerDto>> GetComputer(Guid id)
    {
        var orgId = GetOrganizationId();
        var computer = await _computerService.GetComputerByIdAsync(id, orgId);
        if (computer == null) return NotFound();
        return Ok(computer);
    }

    [HttpPost]
    public async Task<ActionResult<ComputerDto>> CreateComputer([FromBody] CreateComputerRequest request)
    {
        var orgId = GetOrganizationId();
        var userId = GetUserId().ToString();
        var result = await _computerService.CreateComputerAsync(request, orgId, userId);
        return CreatedAtAction(nameof(GetComputer), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ComputerDto>> UpdateComputer(Guid id, [FromBody] UpdateComputerRequest request)
    {
        var orgId = GetOrganizationId();
        var userId = GetUserId().ToString();
        var result = await _computerService.UpdateComputerAsync(id, request, orgId, userId);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteComputer(Guid id)
    {
        var orgId = GetOrganizationId();
        await _computerService.DeleteComputerAsync(id, orgId);
        return NoContent();
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardStatsDto>> GetDashboardStats()
    {
        var orgId = GetOrganizationId();
        var result = await _computerService.GetDashboardStatsAsync(orgId);
        return Ok(result);
    }

    [HttpGet("{id}/metrics")]
    public async Task<ActionResult<List<MetricsDataDto>>> GetMetrics(Guid id,
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] int maxPoints = 100)
    {
        var fromDate = from ?? DateTime.UtcNow.AddHours(-1);
        var toDate = to ?? DateTime.UtcNow;
        var result = await _monitoringService.GetMetricsAsync(id, fromDate, toDate, maxPoints);
        return Ok(result);
    }

    [HttpGet("{id}/metrics/latest")]
    public async Task<ActionResult<MetricsDataDto>> GetLatestMetrics(Guid id)
    {
        var result = await _monitoringService.GetLatestMetricsAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// Send a command to a computer's agent via SignalR.
    /// </summary>
    [HttpPost("{id}/command")]
    public async Task<ActionResult> SendCommand(Guid id, [FromBody] SendCommandRequest request)
    {
        // Send command to agent via SignalR
        var orgId = GetOrganizationId();
        var computer = await _computerService.GetComputerByIdAsync(id, orgId);
        if (computer == null) return NotFound();
        if (computer.Agent == null) return BadRequest(new { error = "No agent connected to this computer" });
        if (!computer.IsOnline) return BadRequest(new { error = "Computer is offline" });

        var commandId = Guid.NewGuid().ToString();
        await _agentHub.Clients.Group($"agent-{computer.Agent.AgentId}")
            .SendAsync("ExecuteCommand", commandId, request.CommandType, request.Parameters);

        return Ok(new { commandId, status = "Sent" });
    }

    /// <summary>
    /// Request process list from agent.
    /// </summary>
    [HttpPost("{id}/processes/refresh")]
    public async Task<ActionResult> RefreshProcesses(Guid id)
    {
        var orgId = GetOrganizationId();
        var computer = await _computerService.GetComputerByIdAsync(id, orgId);
        if (computer?.Agent == null || !computer.IsOnline) return BadRequest(new { error = "Computer is offline or has no agent" });

        await _agentHub.Clients.Group($"agent-{computer.Agent.AgentId}")
            .SendAsync("RequestProcessList", computer.Id);
        return Ok(new { status = "Requested" });
    }

    /// <summary>
    /// Request service list from agent.
    /// </summary>
    [HttpPost("{id}/services/refresh")]
    public async Task<ActionResult> RefreshServices(Guid id)
    {
        var orgId = GetOrganizationId();
        var computer = await _computerService.GetComputerByIdAsync(id, orgId);
        if (computer?.Agent == null || !computer.IsOnline) return BadRequest(new { error = "Computer is offline or has no agent" });

        await _agentHub.Clients.Group($"agent-{computer.Agent.AgentId}")
            .SendAsync("RequestServiceList", computer.Id);
        return Ok(new { status = "Requested" });
    }

    /// <summary>
    /// Request file listing from agent.
    /// </summary>
    [HttpPost("{id}/files/list")]
    public async Task<ActionResult> ListFiles(Guid id, [FromBody] ListFilesRequest request)
    {
        var orgId = GetOrganizationId();
        var computer = await _computerService.GetComputerByIdAsync(id, orgId);
        if (computer?.Agent == null || !computer.IsOnline) return BadRequest(new { error = "Computer is offline or has no agent" });

        await _agentHub.Clients.Group($"agent-{computer.Agent.AgentId}")
            .SendAsync("RequestFileList", computer.Id, request.Path);
        return Ok(new { status = "Requested" });
    }

    /// <summary>
    /// Request drives from agent.
    /// </summary>
    [HttpPost("{id}/files/drives")]
    public async Task<ActionResult> ListDrives(Guid id)
    {
        var orgId = GetOrganizationId();
        var computer = await _computerService.GetComputerByIdAsync(id, orgId);
        if (computer?.Agent == null || !computer.IsOnline) return BadRequest(new { error = "Computer is offline or has no agent" });

        await _agentHub.Clients.Group($"agent-{computer.Agent.AgentId}")
            .SendAsync("RequestDriveList", computer.Id);
        return Ok(new { status = "Requested" });
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());
    private Guid GetOrganizationId() => Guid.Parse(User.FindFirstValue("organizationId") ?? throw new UnauthorizedAccessException());
}

public record SendCommandRequest(string CommandType, Dictionary<string, string>? Parameters);
public record ListFilesRequest(string Path);

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AlertsController : ControllerBase
{
    private readonly IAlertService _alertService;

    public AlertsController(IAlertService alertService) => _alertService = alertService;

    [HttpGet]
    public async Task<ActionResult<PagedResult<AlertDto>>> GetAlerts(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? severity = null, [FromQuery] string? status = null)
    {
        var orgId = GetOrganizationId();
        return Ok(await _alertService.GetAlertsAsync(orgId, page, pageSize, severity, status));
    }

    [HttpPost("{id}/acknowledge")]
    public async Task<ActionResult> Acknowledge(Guid id)
    {
        await _alertService.AcknowledgeAlertAsync(id, GetUserId().ToString());
        return Ok();
    }

    [HttpPost("{id}/resolve")]
    public async Task<ActionResult> Resolve(Guid id)
    {
        await _alertService.ResolveAlertAsync(id, GetUserId().ToString());
        return Ok();
    }

    [HttpGet("rules")]
    public async Task<ActionResult<List<AlertRuleDto>>> GetRules()
    {
        var orgId = GetOrganizationId();
        return Ok(await _alertService.GetAlertRulesAsync(orgId));
    }

    [HttpPost("rules")]
    public async Task<ActionResult<AlertRuleDto>> CreateRule([FromBody] CreateAlertRuleRequest request)
    {
        var orgId = GetOrganizationId();
        return Ok(await _alertService.CreateAlertRuleAsync(request, orgId));
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());
    private Guid GetOrganizationId() => Guid.Parse(User.FindFirstValue("organizationId") ?? throw new UnauthorizedAccessException());
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AgentsController : ControllerBase
{
    private readonly IAgentService _agentService;

    public AgentsController(IAgentService agentService) => _agentService = agentService;

    /// <summary>
    /// Agent registration endpoint — no auth required (uses enrollment code).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AgentRegistrationResponse>> Register([FromBody] AgentRegistrationRequest request)
    {
        var result = await _agentService.RegisterAgentAsync(request);
        return Ok(result);
    }

    [HttpPost("enrollment-codes")]
    public async Task<ActionResult<EnrollmentCodeDto>> GenerateCode([FromBody] GenerateEnrollmentCodeRequest request)
    {
        var orgId = GetOrganizationId();
        return Ok(await _agentService.GenerateEnrollmentCodeAsync(request, orgId));
    }

    [HttpGet("enrollment-codes")]
    public async Task<ActionResult<PagedResult<EnrollmentCodeDto>>> GetCodes([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var orgId = GetOrganizationId();
        return Ok(await _agentService.GetEnrollmentCodesAsync(orgId, page, pageSize));
    }

    [HttpDelete("enrollment-codes/{id}")]
    public async Task<ActionResult> RevokeCode(Guid id)
    {
        var orgId = GetOrganizationId();
        await _agentService.RevokeEnrollmentCodeAsync(id, orgId);
        return NoContent();
    }

    private Guid GetOrganizationId() => Guid.Parse(User.FindFirstValue("organizationId") ?? throw new UnauthorizedAccessException());
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;

    public AuditController(IAuditService auditService) => _auditService = auditService;

    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> GetLogs(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] string? category = null, [FromQuery] string? search = null,
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var orgId = GetOrganizationId();
        return Ok(await _auditService.GetLogsAsync(orgId, page, pageSize, category, search, from, to));
    }

    private Guid GetOrganizationId() => Guid.Parse(User.FindFirstValue("organizationId") ?? throw new UnauthorizedAccessException());
}
