IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE TABLE [AlertRules] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Metric] nvarchar(100) NOT NULL,
        [Operator] nvarchar(10) NOT NULL,
        [Threshold] float NOT NULL,
        [DurationSeconds] int NOT NULL,
        [Severity] int NOT NULL,
        [IsEnabled] bit NOT NULL,
        [OrganizationId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_AlertRules] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE TABLE [ComputerGroups] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NULL,
        [OrganizationId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_ComputerGroups] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE TABLE [DiscoveryScans] (
        [Id] uniqueidentifier NOT NULL,
        [NetworkRange] nvarchar(50) NOT NULL,
        [NetworkInterface] nvarchar(max) NULL,
        [DiscoveryMethods] nvarchar(max) NOT NULL,
        [StartedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        [DevicesFound] int NOT NULL,
        [WindowsDevicesFound] int NOT NULL,
        [AgentsFound] int NOT NULL,
        [NewComputersFound] int NOT NULL,
        [Status] nvarchar(50) NULL,
        [OrganizationId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_DiscoveryScans] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Title] nvarchar(500) NOT NULL,
        [Message] nvarchar(max) NULL,
        [Type] nvarchar(50) NOT NULL,
        [IsRead] bit NOT NULL,
        [ReadAt] datetime2 NULL,
        [ActionUrl] nvarchar(max) NULL,
        [OrganizationId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE TABLE [Organizations] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Slug] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Organizations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE TABLE [Scripts] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NULL,
        [ScriptType] nvarchar(50) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [Parameters] nvarchar(max) NULL,
        [Author] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [OrganizationId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Scripts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE TABLE [Tags] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Color] nvarchar(20) NULL,
        [OrganizationId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Tags] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE TABLE [DiscoveredDevices] (
        [Id] uniqueidentifier NOT NULL,
        [DiscoveryScanId] uniqueidentifier NOT NULL,
        [IpAddress] nvarchar(45) NULL,
        [MacAddress] nvarchar(17) NULL,
        [Hostname] nvarchar(255) NULL,
        [OsGuess] nvarchar(max) NULL,
        [DiscoveryMethod] int NOT NULL,
        [IsWindows] bit NOT NULL,
        [HasAgent] bit NOT NULL,
        [AgentVersion] nvarchar(max) NULL,
        [ApprovalStatus] int NOT NULL,
        [LinkedComputerId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_DiscoveredDevices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DiscoveredDevices_DiscoveryScans_DiscoveryScanId] FOREIGN KEY ([DiscoveryScanId]) REFERENCES [DiscoveryScans] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE TABLE [Computers] (
        [Id] uniqueidentifier NOT NULL,
        [ComputerName] nvarchar(255) NOT NULL,
        [Hostname] nvarchar(255) NOT NULL,
        [Domain] nvarchar(max) NULL,
        [MachineId] nvarchar(450) NULL,
        [OsName] nvarchar(200) NULL,
        [OsVersion] nvarchar(max) NULL,
        [OsBuild] nvarchar(max) NULL,
        [OsEdition] nvarchar(max) NULL,
        [OsArchitecture] nvarchar(max) NULL,
        [CpuName] nvarchar(200) NULL,
        [CpuCores] int NULL,
        [CpuThreads] int NULL,
        [TotalMemoryBytes] bigint NULL,
        [GpuName] nvarchar(max) NULL,
        [Motherboard] nvarchar(max) NULL,
        [BiosVersion] nvarchar(max) NULL,
        [SerialNumber] nvarchar(max) NULL,
        [IpAddress] nvarchar(45) NULL,
        [MacAddress] nvarchar(17) NULL,
        [PublicIpAddress] nvarchar(max) NULL,
        [Status] int NOT NULL,
        [LastSeenAt] datetime2 NULL,
        [IsOnline] bit NOT NULL,
        [Owner] nvarchar(max) NULL,
        [Department] nvarchar(max) NULL,
        [Location] nvarchar(max) NULL,
        [AssetId] nvarchar(max) NULL,
        [Notes] nvarchar(max) NULL,
        [OrganizationId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Computers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Computers_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE TABLE [EnrollmentCodes] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(100) NOT NULL,
        [Status] int NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [UsedByComputerId] uniqueidentifier NULL,
        [UsedAt] datetime2 NULL,
        [Scope] nvarchar(max) NULL,
        [OrganizationId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_EnrollmentCodes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EnrollmentCodes_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [Username] nvarchar(100) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [FirstName] nvarchar(100) NULL,
        [LastName] nvarchar(100) NULL,
        [Role] int NOT NULL,
        [IsActive] bit NOT NULL,
        [MfaEnabled] bit NOT NULL,
        [MfaSecret] nvarchar(max) NULL,
        [LastLoginAt] datetime2 NULL,
        [LastLoginIp] nvarchar(max) NULL,
        [FailedLoginAttempts] int NOT NULL,
        [LockedUntil] datetime2 NULL,
        [OrganizationId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Users_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE TABLE [Agents] (
        [Id] uniqueidentifier NOT NULL,
        [AgentId] uniqueidentifier NOT NULL,
        [InstallationId] nvarchar(450) NULL,
        [PublicKey] nvarchar(max) NULL,
        [AgentVersion] nvarchar(50) NULL,
        [Status] int NOT NULL,
        [LastHeartbeatAt] datetime2 NULL,
        [LastKnownIp] nvarchar(max) NULL,
        [ConnectionId] nvarchar(max) NULL,
        [ApiToken] nvarchar(500) NULL,
        [ApiTokenExpiresAt] datetime2 NULL,
        [ComputerId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Agents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Agents_Computers_ComputerId] FOREIGN KEY ([ComputerId]) REFERENCES [Computers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE TABLE [Alerts] (
        [Id] uniqueidentifier NOT NULL,
        [ComputerId] uniqueidentifier NOT NULL,
        [Title] nvarchar(500) NOT NULL,
        [Message] nvarchar(max) NULL,
        [Severity] int NOT NULL,
        [Status] int NOT NULL,
        [Category] nvarchar(max) NULL,
        [AcknowledgedAt] datetime2 NULL,
        [AcknowledgedBy] nvarchar(max) NULL,
        [ResolvedAt] datetime2 NULL,
        [ResolvedBy] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Alerts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Alerts_Computers_ComputerId] FOREIGN KEY ([ComputerId]) REFERENCES [Computers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [Username] nvarchar(max) NULL,
        [ComputerId] uniqueidentifier NULL,
        [AgentId] uniqueidentifier NULL,
        [Action] nvarchar(200) NOT NULL,
        [Category] nvarchar(100) NULL,
        [Details] nvarchar(max) NULL,
        [IpAddress] nvarchar(max) NULL,
        [Result] nvarchar(max) NULL,
        [SessionId] nvarchar(max) NULL,
        [CorrelationId] nvarchar(max) NULL,
        [OrganizationId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditLogs_Computers_ComputerId] FOREIGN KEY ([ComputerId]) REFERENCES [Computers] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE TABLE [ComputerGroupMembers] (
        [ComputerGroupId] uniqueidentifier NOT NULL,
        [ComputerId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ComputerGroupMembers] PRIMARY KEY ([ComputerGroupId], [ComputerId]),
        CONSTRAINT [FK_ComputerGroupMembers_ComputerGroups_ComputerGroupId] FOREIGN KEY ([ComputerGroupId]) REFERENCES [ComputerGroups] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ComputerGroupMembers_Computers_ComputerId] FOREIGN KEY ([ComputerId]) REFERENCES [Computers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE TABLE [ComputerTags] (
        [ComputerId] uniqueidentifier NOT NULL,
        [TagId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ComputerTags] PRIMARY KEY ([ComputerId], [TagId]),
        CONSTRAINT [FK_ComputerTags_Computers_ComputerId] FOREIGN KEY ([ComputerId]) REFERENCES [Computers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ComputerTags_Tags_TagId] FOREIGN KEY ([TagId]) REFERENCES [Tags] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE TABLE [MonitoringMetrics] (
        [Id] uniqueidentifier NOT NULL,
        [ComputerId] uniqueidentifier NOT NULL,
        [Timestamp] datetime2 NOT NULL,
        [CpuUsagePercent] float NULL,
        [MemoryUsagePercent] float NULL,
        [MemoryUsedBytes] bigint NULL,
        [MemoryTotalBytes] bigint NULL,
        [DiskUsagePercent] float NULL,
        [DiskUsedBytes] bigint NULL,
        [DiskTotalBytes] bigint NULL,
        [NetworkSentBytesPerSec] float NULL,
        [NetworkReceivedBytesPerSec] float NULL,
        [CpuTemperature] float NULL,
        [GpuUsagePercent] float NULL,
        [GpuTemperature] float NULL,
        [UptimeSeconds] bigint NULL,
        [BatteryPercent] float NULL,
        [IsCharging] bit NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_MonitoringMetrics] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MonitoringMetrics_Computers_ComputerId] FOREIGN KEY ([ComputerId]) REFERENCES [Computers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE TABLE [ScriptExecutions] (
        [Id] uniqueidentifier NOT NULL,
        [ScriptId] uniqueidentifier NOT NULL,
        [ComputerId] uniqueidentifier NOT NULL,
        [Parameters] nvarchar(max) NULL,
        [Output] nvarchar(max) NULL,
        [Error] nvarchar(max) NULL,
        [ExitCode] int NULL,
        [Status] int NOT NULL,
        [StartedAt] datetime2 NULL,
        [CompletedAt] datetime2 NULL,
        [ExecutedBy] nvarchar(max) NULL,
        [OrganizationId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_ScriptExecutions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ScriptExecutions_Computers_ComputerId] FOREIGN KEY ([ComputerId]) REFERENCES [Computers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ScriptExecutions_Scripts_ScriptId] FOREIGN KEY ([ScriptId]) REFERENCES [Scripts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] uniqueidentifier NOT NULL,
        [Token] nvarchar(500) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [RevokedAt] datetime2 NULL,
        [ReplacedByToken] nvarchar(max) NULL,
        [RevokedByIp] nvarchar(max) NULL,
        [CreatedByIp] nvarchar(max) NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Agents_AgentId] ON [Agents] ([AgentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Agents_ComputerId] ON [Agents] ([ComputerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Agents_InstallationId] ON [Agents] ([InstallationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Alerts_ComputerId_Status] ON [Alerts] ([ComputerId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_ComputerId] ON [AuditLogs] ([ComputerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_OrganizationId_CreatedAt] ON [AuditLogs] ([OrganizationId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_UserId] ON [AuditLogs] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ComputerGroupMembers_ComputerId] ON [ComputerGroupMembers] ([ComputerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Computers_ComputerName_OrganizationId] ON [Computers] ([ComputerName], [OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Computers_MachineId] ON [Computers] ([MachineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Computers_OrganizationId] ON [Computers] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ComputerTags_TagId] ON [ComputerTags] ([TagId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_DiscoveredDevices_DiscoveryScanId] ON [DiscoveredDevices] ([DiscoveryScanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_DiscoveredDevices_IpAddress] ON [DiscoveredDevices] ([IpAddress]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EnrollmentCodes_Code] ON [EnrollmentCodes] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EnrollmentCodes_OrganizationId] ON [EnrollmentCodes] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MonitoringMetrics_ComputerId_Timestamp] ON [MonitoringMetrics] ([ComputerId], [Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_IsRead] ON [Notifications] ([UserId], [IsRead]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Organizations_Slug] ON [Organizations] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_Token] ON [RefreshTokens] ([Token]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ScriptExecutions_ComputerId_CreatedAt] ON [ScriptExecutions] ([ComputerId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ScriptExecutions_ScriptId] ON [ScriptExecutions] ([ScriptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tags_Name_OrganizationId] ON [Tags] ([Name], [OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_OrganizationId] ON [Users] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Username_OrganizationId] ON [Users] ([Username], [OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001093600_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001093600_InitialCreate', N'9.0.2');
END;

COMMIT;
GO

