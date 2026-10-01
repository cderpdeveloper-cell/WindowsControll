namespace WindowsControlCenter.Domain.Enums;

public enum ComputerStatus
{
    Discovered = 0,
    PendingApproval = 1,
    Enrolling = 2,
    Registered = 3,
    Online = 4,
    Offline = 5,
    Archived = 6,
    Blocked = 7
}

public enum AgentStatus
{
    Connecting = 0,
    Connected = 1,
    Disconnected = 2,
    Reconnecting = 3,
    AuthenticationFailed = 4,
    Blocked = 5,
    Disabled = 6
}

public enum AlertSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

public enum AlertStatus
{
    Active = 0,
    Acknowledged = 1,
    Resolved = 2,
    Muted = 3
}

public enum JobStatus
{
    Pending = 0,
    Running = 1,
    Success = 2,
    Failed = 3,
    Cancelled = 4,
    Skipped = 5
}

public enum ServiceStartupType
{
    Automatic = 0,
    Manual = 1,
    Disabled = 2,
    AutomaticDelayed = 3
}

public enum ServiceStatus
{
    Running = 0,
    Stopped = 1,
    Paused = 2,
    StartPending = 3,
    StopPending = 4,
    PausePending = 5,
    ContinuePending = 6,
    Unknown = 7
}

public enum DiscoveryMethod
{
    WindowsDiscovery = 0,
    Dns = 1,
    WsDiscovery = 2,
    Arp = 3,
    Icmp = 4,
    Smb = 5,
    AgentReport = 6
}

public enum EnrollmentCodeStatus
{
    Active = 0,
    Used = 1,
    Expired = 2,
    Revoked = 3
}

public enum UserRole
{
    SuperAdmin = 0,
    Admin = 1,
    Operator = 2,
    Viewer = 3
}

public enum CommandType
{
    PowerShell = 0,
    Cmd = 1,
    RestartService = 2,
    StopService = 3,
    StartService = 4,
    KillProcess = 5,
    Restart = 6,
    Shutdown = 7,
    Lock = 8,
    Logoff = 9,
    Custom = 10
}
