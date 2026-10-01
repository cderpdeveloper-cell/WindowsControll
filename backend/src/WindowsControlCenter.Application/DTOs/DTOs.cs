using WindowsControlCenter.Domain.Enums;

namespace WindowsControlCenter.Application.DTOs;

// ========== Auth DTOs ==========
public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Username, string Email, string Password, string? FirstName, string? LastName, string? OrganizationName);
public record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt, UserDto User);
public record RefreshTokenRequest(string RefreshToken);

// ========== User DTOs ==========
public record UserDto(Guid Id, string Username, string Email, string? FirstName, string? LastName, UserRole Role, bool IsActive, DateTime? LastLoginAt, Guid OrganizationId);
public record UpdateUserRequest(string? FirstName, string? LastName, UserRole? Role, bool? IsActive);

// ========== Organization DTOs ==========
public record OrganizationDto(Guid Id, string Name, string Slug, string? Description, bool IsActive, DateTime CreatedAt);

// ========== Computer DTOs ==========
public record ComputerDto(
    Guid Id, string ComputerName, string Hostname, string? Domain, string? MachineId,
    string? OsName, string? OsVersion, string? OsBuild, string? OsEdition, string? OsArchitecture,
    string? CpuName, int? CpuCores, int? CpuThreads, long? TotalMemoryBytes,
    string? GpuName, string? Motherboard, string? BiosVersion, string? SerialNumber,
    string? IpAddress, string? MacAddress, string? PublicIpAddress,
    ComputerStatus Status, DateTime? LastSeenAt, bool IsOnline,
    string? Owner, string? Department, string? Location, string? AssetId, string? Notes,
    AgentSummaryDto? Agent, List<string>? Tags, Guid OrganizationId);

public record ComputerSummaryDto(
    Guid Id, string ComputerName, string? IpAddress, string? OsName,
    ComputerStatus Status, bool IsOnline, DateTime? LastSeenAt,
    string? AgentVersion, double? LastCpuPercent, double? LastMemoryPercent, double? LastDiskPercent);

public record CreateComputerRequest(string ComputerName, string Hostname, string? IpAddress, string? MacAddress, string? Notes);
public record UpdateComputerRequest(string? Owner, string? Department, string? Location, string? AssetId, string? Notes, List<string>? Tags);

// ========== Agent DTOs ==========
public record AgentSummaryDto(Guid AgentId, string? AgentVersion, AgentStatus Status, DateTime? LastHeartbeatAt, string? LastKnownIp);
public record AgentRegistrationRequest(string EnrollmentCode, string ComputerName, string Hostname, string? MachineId, string? InstallationId, string? PublicKey, string? OsName, string? OsVersion, string? OsBuild, string? OsEdition, string? OsArchitecture, string? CpuName, int? CpuCores, long? TotalMemoryBytes, string? IpAddress, string? MacAddress);
public record AgentRegistrationResponse(Guid AgentId, string ApiToken, DateTime ApiTokenExpiresAt, Guid ComputerId);
public record AgentHeartbeatRequest(Guid AgentId, string? IpAddress, string? AgentVersion);

// ========== Monitoring DTOs ==========
public record MetricsDataDto(
    Guid ComputerId, DateTime Timestamp,
    double? CpuUsagePercent, double? MemoryUsagePercent, long? MemoryUsedBytes, long? MemoryTotalBytes,
    double? DiskUsagePercent, long? DiskUsedBytes, long? DiskTotalBytes,
    double? NetworkSentBytesPerSec, double? NetworkReceivedBytesPerSec,
    double? CpuTemperature, double? GpuUsagePercent, double? GpuTemperature,
    long? UptimeSeconds, double? BatteryPercent, bool? IsCharging);

public record DashboardStatsDto(
    int TotalComputers, int OnlineComputers, int OfflineComputers,
    int WarningAlerts, int CriticalAlerts,
    double? AvgCpu, double? AvgMemory, double? AvgDisk,
    List<RecentAlertDto> RecentAlerts, List<ComputerSummaryDto> RecentComputers);

public record RecentAlertDto(Guid Id, string Title, AlertSeverity Severity, AlertStatus Status, string? ComputerName, DateTime CreatedAt);

// ========== Alert DTOs ==========
public record AlertDto(Guid Id, Guid ComputerId, string? ComputerName, string Title, string? Message, AlertSeverity Severity, AlertStatus Status, string? Category, DateTime CreatedAt, DateTime? AcknowledgedAt, string? AcknowledgedBy, DateTime? ResolvedAt, string? ResolvedBy);
public record CreateAlertRuleRequest(string Name, string? Description, string Metric, string Operator, double Threshold, int DurationSeconds, AlertSeverity Severity);
public record AlertRuleDto(Guid Id, string Name, string? Description, string Metric, string Operator, double Threshold, int DurationSeconds, AlertSeverity Severity, bool IsEnabled);

// ========== Enrollment DTOs ==========
public record EnrollmentCodeDto(Guid Id, string Code, EnrollmentCodeStatus Status, DateTime ExpiresAt, DateTime CreatedAt, string? Scope);
public record GenerateEnrollmentCodeRequest(int ExpirationMinutes = 60, string? Scope = null);

// ========== Discovery DTOs ==========
public record StartDiscoveryRequest(string NetworkRange, string? NetworkInterface, List<DiscoveryMethod> Methods);
public record DiscoveryScanDto(Guid Id, string NetworkRange, string? Status, int DevicesFound, int WindowsDevicesFound, int NewComputersFound, DateTime StartedAt, DateTime? CompletedAt);
public record DiscoveredDeviceDto(Guid Id, string? IpAddress, string? MacAddress, string? Hostname, string? OsGuess, DiscoveryMethod DiscoveryMethod, bool IsWindows, bool HasAgent, ComputerStatus ApprovalStatus);
public record ApproveDeviceRequest(Guid DiscoveredDeviceId);

// ========== Audit DTOs ==========
public record AuditLogDto(Guid Id, string? Username, string? ComputerName, string Action, string? Category, string? Details, string? IpAddress, string? Result, DateTime CreatedAt);

// ========== Pagination ==========
public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize, int TotalPages);

// ========== Process DTOs ==========
public record ProcessDto(int Pid, string Name, double CpuPercent, long MemoryBytes, string? User, string? Status, int? ParentPid);

// ========== Service DTOs ==========
public record WindowsServiceDto(string ServiceName, string DisplayName, ServiceStatus Status, ServiceStartupType StartupType, string? Account, string? Description);

// ========== Storage DTOs ==========
public record DiskInfoDto(string DriveLetter, string? Label, string FileSystem, long TotalBytes, long FreeBytes, long UsedBytes, double UsagePercent, string DriveType);

// ========== Network DTOs ==========
public record NetworkInterfaceDto(string Name, string? Description, string Status, string? IpAddress, string? SubnetMask, string? MacAddress, long SpeedBps);

// ========== Software DTOs ==========
public record SoftwareDto(string Name, string? Version, string? Publisher, string? InstallDate, string? Architecture);

// ========== File DTOs ==========
public record FileEntryDto(string Name, string Path, string Type, long? Size, DateTime? Modified, DateTime? Created, string? Permissions);
public record DriveEntryDto(string Name, string Path, long TotalBytes, long FreeBytes, string FileSystem, string DriveType);
