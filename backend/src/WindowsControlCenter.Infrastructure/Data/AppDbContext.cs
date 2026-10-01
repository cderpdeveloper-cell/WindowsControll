using Microsoft.EntityFrameworkCore;
using WindowsControlCenter.Domain.Entities;

namespace WindowsControlCenter.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Computer> Computers => Set<Computer>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ComputerTag> ComputerTags => Set<ComputerTag>();
    public DbSet<ComputerGroup> ComputerGroups => Set<ComputerGroup>();
    public DbSet<ComputerGroupMember> ComputerGroupMembers => Set<ComputerGroupMember>();
    public DbSet<EnrollmentCode> EnrollmentCodes => Set<EnrollmentCode>();
    public DbSet<MonitoringMetric> MonitoringMetrics => Set<MonitoringMetric>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<DiscoveryScan> DiscoveryScans => Set<DiscoveryScan>();
    public DbSet<DiscoveredDevice> DiscoveredDevices => Set<DiscoveredDevice>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Script> Scripts => Set<Script>();
    public DbSet<ScriptExecution> ScriptExecutions => Set<ScriptExecution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Organization
        modelBuilder.Entity<Organization>(e =>
        {
            e.HasIndex(o => o.Slug).IsUnique();
            e.Property(o => o.Name).HasMaxLength(200).IsRequired();
            e.Property(o => o.Slug).HasMaxLength(100).IsRequired();
            e.HasQueryFilter(o => !o.IsDeleted);
        });

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => new { u.Username, u.OrganizationId }).IsUnique();
            e.Property(u => u.Username).HasMaxLength(100).IsRequired();
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(u => u.FirstName).HasMaxLength(100);
            e.Property(u => u.LastName).HasMaxLength(100);
            e.HasOne(u => u.Organization).WithMany(o => o.Users).HasForeignKey(u => u.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(u => !u.IsDeleted);
        });

        // RefreshToken
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasIndex(r => r.Token).IsUnique();
            e.Property(r => r.Token).HasMaxLength(500).IsRequired();
            e.HasOne(r => r.User).WithMany(u => u.RefreshTokens).HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // Computer
        modelBuilder.Entity<Computer>(e =>
        {
            e.HasIndex(c => new { c.ComputerName, c.OrganizationId });
            e.HasIndex(c => c.MachineId);
            e.Property(c => c.ComputerName).HasMaxLength(255).IsRequired();
            e.Property(c => c.Hostname).HasMaxLength(255).IsRequired();
            e.Property(c => c.IpAddress).HasMaxLength(45);
            e.Property(c => c.MacAddress).HasMaxLength(17);
            e.Property(c => c.OsName).HasMaxLength(200);
            e.Property(c => c.CpuName).HasMaxLength(200);
            e.HasOne(c => c.Organization).WithMany(o => o.Computers).HasForeignKey(c => c.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(c => !c.IsDeleted);
        });

        // Agent
        modelBuilder.Entity<Agent>(e =>
        {
            e.HasIndex(a => a.AgentId).IsUnique();
            e.HasIndex(a => a.InstallationId);
            e.Property(a => a.AgentVersion).HasMaxLength(50);
            e.Property(a => a.ApiToken).HasMaxLength(500);
            e.HasOne(a => a.Computer).WithOne(c => c.Agent).HasForeignKey<Agent>(a => a.ComputerId).OnDelete(DeleteBehavior.Cascade);
        });

        // Tag
        modelBuilder.Entity<Tag>(e =>
        {
            e.HasIndex(t => new { t.Name, t.OrganizationId }).IsUnique();
            e.Property(t => t.Name).HasMaxLength(100).IsRequired();
            e.Property(t => t.Color).HasMaxLength(20);
            e.HasQueryFilter(t => !t.IsDeleted);
        });

        // ComputerTag (join table)
        modelBuilder.Entity<ComputerTag>(e =>
        {
            e.HasKey(ct => new { ct.ComputerId, ct.TagId });
            e.HasOne(ct => ct.Computer).WithMany(c => c.ComputerTags).HasForeignKey(ct => ct.ComputerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ct => ct.Tag).WithMany(t => t.ComputerTags).HasForeignKey(ct => ct.TagId).OnDelete(DeleteBehavior.Cascade);
        });

        // ComputerGroup
        modelBuilder.Entity<ComputerGroup>(e =>
        {
            e.Property(g => g.Name).HasMaxLength(200).IsRequired();
            e.HasQueryFilter(g => !g.IsDeleted);
        });

        // ComputerGroupMember
        modelBuilder.Entity<ComputerGroupMember>(e =>
        {
            e.HasKey(m => new { m.ComputerGroupId, m.ComputerId });
            e.HasOne(m => m.ComputerGroup).WithMany(g => g.Members).HasForeignKey(m => m.ComputerGroupId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Computer).WithMany().HasForeignKey(m => m.ComputerId).OnDelete(DeleteBehavior.Cascade);
        });

        // EnrollmentCode
        modelBuilder.Entity<EnrollmentCode>(e =>
        {
            e.HasIndex(ec => ec.Code).IsUnique();
            e.Property(ec => ec.Code).HasMaxLength(100).IsRequired();
            e.HasOne(ec => ec.Organization).WithMany().HasForeignKey(ec => ec.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(ec => !ec.IsDeleted);
        });

        // MonitoringMetric
        modelBuilder.Entity<MonitoringMetric>(e =>
        {
            e.HasIndex(m => new { m.ComputerId, m.Timestamp });
            e.HasOne(m => m.Computer).WithMany(c => c.Metrics).HasForeignKey(m => m.ComputerId).OnDelete(DeleteBehavior.Cascade);
        });

        // Alert
        modelBuilder.Entity<Alert>(e =>
        {
            e.HasIndex(a => new { a.ComputerId, a.Status });
            e.Property(a => a.Title).HasMaxLength(500).IsRequired();
            e.HasOne(a => a.Computer).WithMany(c => c.Alerts).HasForeignKey(a => a.ComputerId).OnDelete(DeleteBehavior.Cascade);
        });

        // AlertRule
        modelBuilder.Entity<AlertRule>(e =>
        {
            e.Property(r => r.Name).HasMaxLength(200).IsRequired();
            e.Property(r => r.Metric).HasMaxLength(100).IsRequired();
            e.Property(r => r.Operator).HasMaxLength(10).IsRequired();
        });

        // DiscoveryScan
        modelBuilder.Entity<DiscoveryScan>(e =>
        {
            e.Property(s => s.NetworkRange).HasMaxLength(50).IsRequired();
            e.Property(s => s.Status).HasMaxLength(50);
        });

        // DiscoveredDevice
        modelBuilder.Entity<DiscoveredDevice>(e =>
        {
            e.HasIndex(d => d.IpAddress);
            e.Property(d => d.IpAddress).HasMaxLength(45);
            e.Property(d => d.MacAddress).HasMaxLength(17);
            e.Property(d => d.Hostname).HasMaxLength(255);
            e.HasOne(d => d.DiscoveryScan).WithMany(s => s.DiscoveredDevices).HasForeignKey(d => d.DiscoveryScanId).OnDelete(DeleteBehavior.Cascade);
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasIndex(a => new { a.OrganizationId, a.CreatedAt });
            e.HasIndex(a => a.UserId);
            e.Property(a => a.Action).HasMaxLength(200).IsRequired();
            e.Property(a => a.Category).HasMaxLength(100);
            e.HasOne(a => a.Computer).WithMany(c => c.AuditLogs).HasForeignKey(a => a.ComputerId).OnDelete(DeleteBehavior.SetNull);
        });

        // Notification
        modelBuilder.Entity<Notification>(e =>
        {
            e.HasIndex(n => new { n.UserId, n.IsRead });
            e.Property(n => n.Title).HasMaxLength(500).IsRequired();
            e.Property(n => n.Type).HasMaxLength(50);
        });

        // Script
        modelBuilder.Entity<Script>(e =>
        {
            e.Property(s => s.Name).HasMaxLength(200).IsRequired();
            e.Property(s => s.ScriptType).HasMaxLength(50).IsRequired();
            e.HasQueryFilter(s => !s.IsDeleted);
        });

        // ScriptExecution
        modelBuilder.Entity<ScriptExecution>(e =>
        {
            e.HasIndex(se => new { se.ComputerId, se.CreatedAt });
            e.HasOne(se => se.Script).WithMany().HasForeignKey(se => se.ScriptId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(se => se.Computer).WithMany().HasForeignKey(se => se.ComputerId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    public override int SaveChanges()
    {
        UpdateAuditFields();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateAuditFields()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is Domain.Common.BaseEntity && (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            var entity = (Domain.Common.BaseEntity)entry.Entity;

            if (entry.State == EntityState.Added)
            {
                entity.CreatedAt = DateTime.UtcNow;
            }

            entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
