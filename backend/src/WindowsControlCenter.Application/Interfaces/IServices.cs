using WindowsControlCenter.Application.DTOs;
using WindowsControlCenter.Domain.Enums;

namespace WindowsControlCenter.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress);
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string ipAddress);
    Task<AuthResponse> RefreshTokenAsync(string token, string ipAddress);
    Task RevokeTokenAsync(string token, string ipAddress);
    Task<UserDto?> GetCurrentUserAsync(Guid userId);
}

public interface IUserService
{
    Task<PagedResult<UserDto>> GetUsersAsync(Guid organizationId, int page = 1, int pageSize = 20, string? search = null);
    Task<UserDto?> GetUserByIdAsync(Guid id, Guid organizationId);
    Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequest request, Guid organizationId);
    Task DeleteUserAsync(Guid id, Guid organizationId);
}

public interface IComputerService
{
    Task<PagedResult<ComputerSummaryDto>> GetComputersAsync(Guid organizationId, int page = 1, int pageSize = 20,
        string? search = null, string? status = null, string? sortBy = null, bool sortDesc = false);
    Task<ComputerDto?> GetComputerByIdAsync(Guid id, Guid organizationId);
    Task<ComputerDto> CreateComputerAsync(CreateComputerRequest request, Guid organizationId, string userId);
    Task<ComputerDto> UpdateComputerAsync(Guid id, UpdateComputerRequest request, Guid organizationId, string userId);
    Task DeleteComputerAsync(Guid id, Guid organizationId);
    Task UpdateComputerStatusAsync(Guid computerId, bool isOnline);
    Task<DashboardStatsDto> GetDashboardStatsAsync(Guid organizationId);
}

public interface IAgentService
{
    Task<AgentRegistrationResponse> RegisterAgentAsync(AgentRegistrationRequest request);
    Task<bool> ValidateAgentTokenAsync(Guid agentId, string apiToken);
    Task ProcessHeartbeatAsync(AgentHeartbeatRequest request);
    Task UpdateAgentConnectionAsync(Guid agentId, string? connectionId, AgentStatus status);
    Task<EnrollmentCodeDto> GenerateEnrollmentCodeAsync(GenerateEnrollmentCodeRequest request, Guid organizationId);
    Task<PagedResult<EnrollmentCodeDto>> GetEnrollmentCodesAsync(Guid organizationId, int page = 1, int pageSize = 20);
    Task RevokeEnrollmentCodeAsync(Guid codeId, Guid organizationId);
}

public interface IMonitoringService
{
    Task StoreMetricsAsync(MetricsDataDto metrics);
    Task<List<MetricsDataDto>> GetMetricsAsync(Guid computerId, DateTime from, DateTime to, int maxPoints = 100);
    Task<MetricsDataDto?> GetLatestMetricsAsync(Guid computerId);
}

public interface IAlertService
{
    Task<PagedResult<AlertDto>> GetAlertsAsync(Guid organizationId, int page = 1, int pageSize = 20,
        string? severity = null, string? status = null);
    Task<AlertDto> CreateAlertAsync(Guid computerId, string title, string? message, AlertSeverity severity, string? category = null);
    Task AcknowledgeAlertAsync(Guid alertId, string userId);
    Task ResolveAlertAsync(Guid alertId, string userId);
    Task EvaluateAlertsAsync(Guid computerId, MetricsDataDto metrics);
    Task<List<AlertRuleDto>> GetAlertRulesAsync(Guid organizationId);
    Task<AlertRuleDto> CreateAlertRuleAsync(CreateAlertRuleRequest request, Guid organizationId);
}

public interface IDiscoveryService
{
    Task<DiscoveryScanDto> StartDiscoveryAsync(StartDiscoveryRequest request, Guid organizationId);
    Task<DiscoveryScanDto?> GetScanStatusAsync(Guid scanId);
    Task<List<DiscoveredDeviceDto>> GetDiscoveredDevicesAsync(Guid scanId);
    Task<ComputerDto> ApproveDeviceAsync(ApproveDeviceRequest request, Guid organizationId, string userId);
}

public interface IAuditService
{
    Task LogAsync(string action, string? category = null, string? details = null,
        Guid? userId = null, string? username = null, Guid? computerId = null,
        string? ipAddress = null, string? result = null, Guid? organizationId = null);
    Task<PagedResult<AuditLogDto>> GetLogsAsync(Guid organizationId, int page = 1, int pageSize = 50,
        string? category = null, string? search = null, DateTime? from = null, DateTime? to = null);
}
