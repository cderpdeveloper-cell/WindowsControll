using WindowsControlCenter.Domain.Common;
using WindowsControlCenter.Domain.Enums;

namespace WindowsControlCenter.Domain.Entities;

/// <summary>
/// Represents an organization (tenant) in the multi-tenant system.
/// </summary>
public class Organization : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Computer> Computers { get; set; } = new List<Computer>();
}

/// <summary>
/// Represents a user with authentication and RBAC.
/// </summary>
public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public UserRole Role { get; set; } = UserRole.Viewer;
    public bool IsActive { get; set; } = true;
    public bool MfaEnabled { get; set; }
    public string? MfaSecret { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }

    // Multi-tenant
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    // Navigation
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

/// <summary>
/// JWT refresh token for token rotation.
/// </summary>
public class RefreshToken : BaseEntity
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }
    public string? RevokedByIp { get; set; }
    public string CreatedByIp { get; set; } = string.Empty;
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt != null;
    public bool IsActive => !IsRevoked && !IsExpired;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}

/// <summary>
/// Represents a managed Windows computer.
/// </summary>
public class Computer : BaseEntity
{
    // Identity — never use IP as primary
    public string ComputerName { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string? MachineId { get; set; }

    // OS Information
    public string? OsName { get; set; }
    public string? OsVersion { get; set; }
    public string? OsBuild { get; set; }
    public string? OsEdition { get; set; }
    public string? OsArchitecture { get; set; }

    // Hardware
    public string? CpuName { get; set; }
    public int? CpuCores { get; set; }
    public int? CpuThreads { get; set; }
    public long? TotalMemoryBytes { get; set; }
    public string? GpuName { get; set; }
    public string? Motherboard { get; set; }
    public string? BiosVersion { get; set; }
    public string? SerialNumber { get; set; }

    // Network
    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public string? PublicIpAddress { get; set; }

    // Status
    public ComputerStatus Status { get; set; } = ComputerStatus.Discovered;
    public DateTime? LastSeenAt { get; set; }
    public bool IsOnline { get; set; }

    // Metadata
    public string? Owner { get; set; }
    public string? Department { get; set; }
    public string? Location { get; set; }
    public string? AssetId { get; set; }
    public string? Notes { get; set; }

    // Multi-tenant
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    // Navigation
    public Agent? Agent { get; set; }
    public ICollection<ComputerTag> ComputerTags { get; set; } = new List<ComputerTag>();
    public ICollection<MonitoringMetric> Metrics { get; set; } = new List<MonitoringMetric>();
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}

/// <summary>
/// Represents an agent installed on a managed computer.
/// </summary>
public class Agent : BaseEntity
{
    public Guid AgentId { get; set; } = Guid.NewGuid();
    public string? InstallationId { get; set; }
    public string? PublicKey { get; set; }
    public string? AgentVersion { get; set; }
    public AgentStatus Status { get; set; } = AgentStatus.Disconnected;
    public DateTime? LastHeartbeatAt { get; set; }
    public string? LastKnownIp { get; set; }
    public string? ConnectionId { get; set; }

    // Authentication
    public string? ApiToken { get; set; }
    public DateTime? ApiTokenExpiresAt { get; set; }

    // Computer link
    public Guid ComputerId { get; set; }
    public Computer Computer { get; set; } = null!;
}

/// <summary>
/// Tag for categorizing computers.
/// </summary>
public class Tag : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public Guid OrganizationId { get; set; }
    public ICollection<ComputerTag> ComputerTags { get; set; } = new List<ComputerTag>();
}

/// <summary>
/// Many-to-many join between Computer and Tag.
/// </summary>
public class ComputerTag
{
    public Guid ComputerId { get; set; }
    public Computer Computer { get; set; } = null!;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}

/// <summary>
/// Enrollment code for agent registration.
/// </summary>
public class EnrollmentCode : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public EnrollmentCodeStatus Status { get; set; } = EnrollmentCodeStatus.Active;
    public DateTime ExpiresAt { get; set; }
    public Guid? UsedByComputerId { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? Scope { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
}

/// <summary>
/// Real-time and historical monitoring metrics.
/// </summary>
public class MonitoringMetric : BaseEntity
{
    public Guid ComputerId { get; set; }
    public Computer Computer { get; set; } = null!;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double? CpuUsagePercent { get; set; }
    public double? MemoryUsagePercent { get; set; }
    public long? MemoryUsedBytes { get; set; }
    public long? MemoryTotalBytes { get; set; }
    public double? DiskUsagePercent { get; set; }
    public long? DiskUsedBytes { get; set; }
    public long? DiskTotalBytes { get; set; }
    public double? NetworkSentBytesPerSec { get; set; }
    public double? NetworkReceivedBytesPerSec { get; set; }
    public double? CpuTemperature { get; set; }
    public double? GpuUsagePercent { get; set; }
    public double? GpuTemperature { get; set; }
    public long? UptimeSeconds { get; set; }
    public double? BatteryPercent { get; set; }
    public bool? IsCharging { get; set; }
}

/// <summary>
/// Alert generated by monitoring rules.
/// </summary>
public class Alert : BaseEntity
{
    public Guid ComputerId { get; set; }
    public Computer Computer { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public AlertSeverity Severity { get; set; }
    public AlertStatus Status { get; set; } = AlertStatus.Active;
    public string? Category { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
}

/// <summary>
/// Alert rule for generating alerts from monitoring data.
/// </summary>
public class AlertRule : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Metric { get; set; } = string.Empty; // e.g., "CpuUsagePercent"
    public string Operator { get; set; } = string.Empty; // >, <, >=, <=, ==
    public double Threshold { get; set; }
    public int DurationSeconds { get; set; } = 60;
    public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;
    public bool IsEnabled { get; set; } = true;
    public Guid OrganizationId { get; set; }
}

/// <summary>
/// Discovery scan record.
/// </summary>
public class DiscoveryScan : BaseEntity
{
    public string NetworkRange { get; set; } = string.Empty;
    public string? NetworkInterface { get; set; }
    public string DiscoveryMethods { get; set; } = string.Empty; // comma-separated
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int DevicesFound { get; set; }
    public int WindowsDevicesFound { get; set; }
    public int AgentsFound { get; set; }
    public int NewComputersFound { get; set; }
    public string? Status { get; set; } // Running, Completed, Failed
    public Guid OrganizationId { get; set; }
    public ICollection<DiscoveredDevice> DiscoveredDevices { get; set; } = new List<DiscoveredDevice>();
}

/// <summary>
/// Device found during network discovery.
/// </summary>
public class DiscoveredDevice : BaseEntity
{
    public Guid DiscoveryScanId { get; set; }
    public DiscoveryScan DiscoveryScan { get; set; } = null!;

    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public string? Hostname { get; set; }
    public string? OsGuess { get; set; }
    public DiscoveryMethod DiscoveryMethod { get; set; }
    public bool IsWindows { get; set; }
    public bool HasAgent { get; set; }
    public string? AgentVersion { get; set; }
    public ComputerStatus ApprovalStatus { get; set; } = ComputerStatus.Discovered;
    public Guid? LinkedComputerId { get; set; }
}

/// <summary>
/// Audit log entry for tracking all actions.
/// </summary>
public class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public Guid? ComputerId { get; set; }
    public Computer? Computer { get; set; }
    public Guid? AgentId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public string? Result { get; set; }
    public string? SessionId { get; set; }
    public string? CorrelationId { get; set; }
    public Guid OrganizationId { get; set; }
}

/// <summary>
/// Notification for users.
/// </summary>
public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string Type { get; set; } = "Info"; // Critical, Warning, Info, Success
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? ActionUrl { get; set; }
    public Guid OrganizationId { get; set; }
}

/// <summary>
/// Computer group for organizing computers.
/// </summary>
public class ComputerGroup : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid OrganizationId { get; set; }
    public ICollection<ComputerGroupMember> Members { get; set; } = new List<ComputerGroupMember>();
}

public class ComputerGroupMember
{
    public Guid ComputerGroupId { get; set; }
    public ComputerGroup ComputerGroup { get; set; } = null!;
    public Guid ComputerId { get; set; }
    public Computer Computer { get; set; } = null!;
}

/// <summary>
/// Script stored in the script library.
/// </summary>
public class Script : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ScriptType { get; set; } = "PowerShell"; // PowerShell, CMD, Batch
    public string Content { get; set; } = string.Empty;
    public string? Parameters { get; set; } // JSON schema for parameters
    public string? Author { get; set; }
    public int Version { get; set; } = 1;
    public Guid OrganizationId { get; set; }
}

/// <summary>
/// Record of a script execution.
/// </summary>
public class ScriptExecution : BaseEntity
{
    public Guid ScriptId { get; set; }
    public Script Script { get; set; } = null!;
    public Guid ComputerId { get; set; }
    public Computer Computer { get; set; } = null!;
    public string? Parameters { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public int? ExitCode { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ExecutedBy { get; set; }
    public Guid OrganizationId { get; set; }
}
