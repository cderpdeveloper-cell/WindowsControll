using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WindowsControlCenter.Application.DTOs;
using WindowsControlCenter.Application.Interfaces;
using WindowsControlCenter.Domain.Entities;
using WindowsControlCenter.Domain.Enums;
using WindowsControlCenter.Infrastructure.Data;

namespace WindowsControlCenter.Infrastructure.Services;

public class AgentService : IAgentService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ILogger<AgentService> _logger;

    public AgentService(AppDbContext db, IAuditService audit, ILogger<AgentService> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public async Task<AgentRegistrationResponse> RegisterAgentAsync(AgentRegistrationRequest request)
    {
        // Validate enrollment code
        var enrollmentCode = await _db.EnrollmentCodes
            .FirstOrDefaultAsync(ec => ec.Code == request.EnrollmentCode && ec.Status == EnrollmentCodeStatus.Active);

        if (enrollmentCode == null)
            throw new UnauthorizedAccessException("Invalid enrollment code.");

        if (enrollmentCode.ExpiresAt < DateTime.UtcNow)
        {
            enrollmentCode.Status = EnrollmentCodeStatus.Expired;
            await _db.SaveChangesAsync();
            throw new UnauthorizedAccessException("Enrollment code has expired.");
        }

        // Check for existing computer by identity (prevent duplicates)
        Computer? existingComputer = null;

        if (!string.IsNullOrEmpty(request.InstallationId))
        {
            existingComputer = await _db.Computers.Include(c => c.Agent)
                .FirstOrDefaultAsync(c => c.Agent != null && c.Agent.InstallationId == request.InstallationId);
        }

        if (existingComputer == null && !string.IsNullOrEmpty(request.MachineId))
        {
            existingComputer = await _db.Computers.Include(c => c.Agent)
                .FirstOrDefaultAsync(c => c.MachineId == request.MachineId && c.OrganizationId == enrollmentCode.OrganizationId);
        }

        if (existingComputer == null && !string.IsNullOrEmpty(request.MacAddress))
        {
            existingComputer = await _db.Computers.Include(c => c.Agent)
                .FirstOrDefaultAsync(c => c.MacAddress == request.MacAddress && c.OrganizationId == enrollmentCode.OrganizationId);
        }

        Computer computer;
        Agent agent;

        if (existingComputer != null)
        {
            // Update existing computer
            computer = existingComputer;
            UpdateComputerFromRequest(computer, request);

            if (computer.Agent != null)
            {
                agent = computer.Agent;
                agent.ApiToken = GenerateSecureToken();
                agent.ApiTokenExpiresAt = DateTime.UtcNow.AddDays(365);
                agent.Status = AgentStatus.Connected;
                agent.LastHeartbeatAt = DateTime.UtcNow;
                agent.LastKnownIp = request.IpAddress;
                agent.InstallationId = request.InstallationId;
                agent.PublicKey = request.PublicKey;
            }
            else
            {
                agent = CreateAgent(computer, request);
                _db.Agents.Add(agent);
            }

            _logger.LogInformation("Re-registered agent for existing computer {ComputerName} ({ComputerId})",
                computer.ComputerName, computer.Id);
        }
        else
        {
            // Create new computer
            computer = new Computer
            {
                ComputerName = request.ComputerName,
                Hostname = request.Hostname,
                MachineId = request.MachineId,
                OrganizationId = enrollmentCode.OrganizationId,
                Status = ComputerStatus.Registered,
                IsOnline = true,
                LastSeenAt = DateTime.UtcNow
            };

            UpdateComputerFromRequest(computer, request);
            _db.Computers.Add(computer);
            await _db.SaveChangesAsync(); // Get computer ID

            agent = CreateAgent(computer, request);
            _db.Agents.Add(agent);

            _logger.LogInformation("Registered new computer {ComputerName} ({ComputerId})",
                computer.ComputerName, computer.Id);
        }

        // Mark enrollment code as used
        enrollmentCode.Status = EnrollmentCodeStatus.Used;
        enrollmentCode.UsedByComputerId = computer.Id;
        enrollmentCode.UsedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _audit.LogAsync("Agent.Register", "Agent",
            $"Agent registered for {computer.ComputerName}",
            computerId: computer.Id, ipAddress: request.IpAddress,
            result: "Success", organizationId: enrollmentCode.OrganizationId);

        return new AgentRegistrationResponse(agent.AgentId, agent.ApiToken!, agent.ApiTokenExpiresAt!.Value, computer.Id);
    }

    public async Task<bool> ValidateAgentTokenAsync(Guid agentId, string apiToken)
    {
        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.AgentId == agentId);
        if (agent == null) return false;
        if (agent.ApiToken != apiToken) return false;
        if (agent.ApiTokenExpiresAt.HasValue && agent.ApiTokenExpiresAt < DateTime.UtcNow) return false;
        if (agent.Status == AgentStatus.Blocked || agent.Status == AgentStatus.Disabled) return false;
        return true;
    }

    public async Task ProcessHeartbeatAsync(AgentHeartbeatRequest request)
    {
        var agent = await _db.Agents.Include(a => a.Computer)
            .FirstOrDefaultAsync(a => a.AgentId == request.AgentId);

        if (agent == null)
        {
            _logger.LogWarning("Heartbeat from unknown agent {AgentId}", request.AgentId);
            return;
        }

        agent.LastHeartbeatAt = DateTime.UtcNow;
        agent.Status = AgentStatus.Connected;

        if (!string.IsNullOrEmpty(request.IpAddress))
        {
            agent.LastKnownIp = request.IpAddress;
            // Update computer IP if changed
            if (agent.Computer.IpAddress != request.IpAddress)
            {
                _logger.LogInformation("IP changed for {ComputerName}: {OldIp} → {NewIp}",
                    agent.Computer.ComputerName, agent.Computer.IpAddress, request.IpAddress);
                agent.Computer.IpAddress = request.IpAddress;
            }
        }

        if (!string.IsNullOrEmpty(request.AgentVersion))
            agent.AgentVersion = request.AgentVersion;

        agent.Computer.IsOnline = true;
        agent.Computer.LastSeenAt = DateTime.UtcNow;
        agent.Computer.Status = ComputerStatus.Online;

        await _db.SaveChangesAsync();
    }

    public async Task UpdateAgentConnectionAsync(Guid agentId, string? connectionId, AgentStatus status)
    {
        var agent = await _db.Agents.Include(a => a.Computer)
            .FirstOrDefaultAsync(a => a.AgentId == agentId);

        if (agent == null) return;

        agent.ConnectionId = connectionId;
        agent.Status = status;

        if (status == AgentStatus.Disconnected)
        {
            agent.Computer.IsOnline = false;
            agent.Computer.Status = ComputerStatus.Offline;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<EnrollmentCodeDto> GenerateEnrollmentCodeAsync(GenerateEnrollmentCodeRequest request, Guid organizationId)
    {
        var code = new EnrollmentCode
        {
            Code = GenerateEnrollmentCodeString(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(request.ExpirationMinutes),
            OrganizationId = organizationId,
            Scope = request.Scope,
            Status = EnrollmentCodeStatus.Active
        };

        _db.EnrollmentCodes.Add(code);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("EnrollmentCode.Create", "Agent",
            "Generated enrollment code", organizationId: organizationId, result: "Success");

        return new EnrollmentCodeDto(code.Id, code.Code, code.Status, code.ExpiresAt, code.CreatedAt, code.Scope);
    }

    public async Task<PagedResult<EnrollmentCodeDto>> GetEnrollmentCodesAsync(Guid organizationId, int page = 1, int pageSize = 20)
    {
        var query = _db.EnrollmentCodes.Where(ec => ec.OrganizationId == organizationId);
        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(ec => ec.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ec => new EnrollmentCodeDto(ec.Id, ec.Code, ec.Status, ec.ExpiresAt, ec.CreatedAt, ec.Scope))
            .ToListAsync();

        return new PagedResult<EnrollmentCodeDto>(items, totalCount, page, pageSize, (int)Math.Ceiling((double)totalCount / pageSize));
    }

    public async Task RevokeEnrollmentCodeAsync(Guid codeId, Guid organizationId)
    {
        var code = await _db.EnrollmentCodes
            .FirstOrDefaultAsync(ec => ec.Id == codeId && ec.OrganizationId == organizationId)
            ?? throw new KeyNotFoundException("Enrollment code not found.");

        code.Status = EnrollmentCodeStatus.Revoked;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("EnrollmentCode.Revoke", "Agent",
            "Revoked enrollment code", organizationId: organizationId, result: "Success");
    }

    private static void UpdateComputerFromRequest(Computer computer, AgentRegistrationRequest request)
    {
        computer.Hostname = request.Hostname;
        if (!string.IsNullOrEmpty(request.OsName)) computer.OsName = request.OsName;
        if (!string.IsNullOrEmpty(request.OsVersion)) computer.OsVersion = request.OsVersion;
        if (!string.IsNullOrEmpty(request.OsBuild)) computer.OsBuild = request.OsBuild;
        if (!string.IsNullOrEmpty(request.OsEdition)) computer.OsEdition = request.OsEdition;
        if (!string.IsNullOrEmpty(request.OsArchitecture)) computer.OsArchitecture = request.OsArchitecture;
        if (!string.IsNullOrEmpty(request.CpuName)) computer.CpuName = request.CpuName;
        if (request.CpuCores.HasValue) computer.CpuCores = request.CpuCores;
        if (request.TotalMemoryBytes.HasValue) computer.TotalMemoryBytes = request.TotalMemoryBytes;
        if (!string.IsNullOrEmpty(request.IpAddress)) computer.IpAddress = request.IpAddress;
        if (!string.IsNullOrEmpty(request.MacAddress)) computer.MacAddress = request.MacAddress;
        if (!string.IsNullOrEmpty(request.MachineId)) computer.MachineId = request.MachineId;
    }

    private Agent CreateAgent(Computer computer, AgentRegistrationRequest request)
    {
        return new Agent
        {
            ComputerId = computer.Id,
            InstallationId = request.InstallationId,
            PublicKey = request.PublicKey,
            ApiToken = GenerateSecureToken(),
            ApiTokenExpiresAt = DateTime.UtcNow.AddDays(365),
            Status = AgentStatus.Connected,
            LastHeartbeatAt = DateTime.UtcNow,
            LastKnownIp = request.IpAddress
        };
    }

    private static string GenerateSecureToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    private static string GenerateEnrollmentCodeString()
    {
        var bytes = RandomNumberGenerator.GetBytes(10);
        var code = Convert.ToBase64String(bytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .ToUpperInvariant();
        // Format as XXXXX-XXXXX
        return code.Length >= 10
            ? $"{code[..5]}-{code[5..10]}"
            : code;
    }
}
