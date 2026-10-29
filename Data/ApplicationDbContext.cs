using Microsoft.EntityFrameworkCore;
using tran_nguyen_vi_khang_api.Models;

namespace tran_nguyen_vi_khang_api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Child> Children => Set<Child>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<MonitoringRule> MonitoringRules => Set<MonitoringRule>();
    public DbSet<EmailOtp> EmailOtps => Set<EmailOtp>();
    public DbSet<DeviceLocation> DeviceLocations => Set<DeviceLocation>();
    public DbSet<LocationRefreshRequest> LocationRefreshRequests => Set<LocationRefreshRequest>();
    public DbSet<DeviceModeRequest> DeviceModeRequests => Set<DeviceModeRequest>();
    public DbSet<DeviceActivityLog> DeviceActivityLogs => Set<DeviceActivityLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);

            entity.Property(u => u.FullName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(u => u.Email)
                .HasMaxLength(255)
                .IsRequired();

            entity.HasIndex(u => u.Email)
                .IsUnique();

            entity.Property(u => u.PasswordHash)
                .HasMaxLength(500)
                .IsRequired(false);

            entity.Property(u => u.GoogleSubjectId)
                .HasMaxLength(255);

            entity.HasIndex(u => u.GoogleSubjectId);

            entity.Property(u => u.Role)
                .HasMaxLength(20)
                .IsRequired();
        });

        modelBuilder.Entity<Child>(entity =>
        {
            entity.Property(c => c.FullName)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasOne(c => c.Parent)
                .WithMany(u => u.Children)
                .HasForeignKey(c => c.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Device>(entity =>
        {
            entity.Property(d => d.DeviceName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(d => d.DeviceIdentifier)
                .HasMaxLength(255);

            entity.Property(d => d.Platform)
                .HasMaxLength(50);

            entity.HasOne(d => d.Child)
                .WithMany(c => c.Devices)
                .HasForeignKey(d => d.ChildId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MonitoringRule>(entity =>
        {
            entity.Property(r => r.RuleName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(r => r.RuleType)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(r => r.Description)
                .HasMaxLength(500);

            entity.Property(r => r.RuleConfig)
                .HasMaxLength(4000);

            entity.Property(r => r.SyncStatus)
                .HasMaxLength(50)
                .HasDefaultValue("ServerSaved");

            entity.Property(r => r.Version)
                .HasDefaultValue(1);

            entity.HasOne(r => r.Child)
                .WithMany(c => c.MonitoringRules)
                .HasForeignKey(r => r.ChildId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceActivityLog>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.Property(a => a.ClientEventId)
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(a => a.EventType)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(a => a.PackageName)
                .HasMaxLength(255);

            entity.Property(a => a.AppName)
                .HasMaxLength(255);

            entity.Property(a => a.ViolationAction)
                .HasMaxLength(50);

            entity.Property(a => a.ViolationDetails)
                .HasMaxLength(500);

            entity.HasOne(a => a.Device)
                .WithMany()
                .HasForeignKey(a => a.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Child)
                .WithMany()
                .HasForeignKey(a => a.ChildId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Rule)
                .WithMany()
                .HasForeignKey(a => a.RuleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(a => a.ClientEventId)
                .IsUnique();

            entity.HasIndex(a => new { a.ChildId, a.RecordedAt });
            entity.HasIndex(a => new { a.DeviceId, a.RecordedAt });
            entity.HasIndex(a => new { a.ChildId, a.EventType, a.RecordedAt });
        });

        modelBuilder.Entity<EmailOtp>(entity =>
        {
            entity.Property(o => o.OtpHash)
                .HasMaxLength(500)
                .IsRequired();

            entity.HasOne(o => o.User)
                .WithMany(u => u.EmailOtps)
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(o => new { o.UserId, o.ExpiresAt });
        });

        modelBuilder.Entity<DeviceLocation>(entity =>
        {
            entity.HasKey(dl => dl.Id);

            entity.Property(dl => dl.EventType)
                .HasMaxLength(50)
                .HasDefaultValue("Periodic");

            entity.HasOne(dl => dl.Device)
                .WithMany()
                .HasForeignKey(dl => dl.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(dl => dl.Child)
                .WithMany()
                .HasForeignKey(dl => dl.ChildId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(dl => new { dl.ChildId, dl.RecordedAt });
            entity.HasIndex(dl => dl.DeviceId);
        });

        modelBuilder.Entity<LocationRefreshRequest>(entity =>
        {
            entity.HasKey(lr => lr.Id);

            entity.Property(lr => lr.Status)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(lr => lr.FailureReason)
                .HasMaxLength(500);

            entity.HasOne(lr => lr.Device)
                .WithMany()
                .HasForeignKey(lr => lr.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(lr => lr.Child)
                .WithMany()
                .HasForeignKey(lr => lr.ChildId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(lr => new { lr.DeviceId, lr.Status, lr.ExpiresAt });
            entity.HasIndex(lr => new { lr.ChildId, lr.RequestedAt });
        });

        modelBuilder.Entity<DeviceModeRequest>(entity =>
        {
            entity.HasKey(dmr => dmr.Id);

            entity.Property(dmr => dmr.TargetMode)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(dmr => dmr.Status)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(dmr => dmr.Reason)
                .HasMaxLength(500);

            entity.Property(dmr => dmr.ApprovalToken)
                .HasMaxLength(100);

            entity.HasOne(dmr => dmr.Device)
                .WithMany()
                .HasForeignKey(dmr => dmr.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(dmr => dmr.Child)
                .WithMany()
                .HasForeignKey(dmr => dmr.ChildId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(dmr => dmr.ReviewedByUser)
                .WithMany()
                .HasForeignKey(dmr => dmr.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(dmr => new { dmr.DeviceId, dmr.Status, dmr.ExpiresAt });
            entity.HasIndex(dmr => new { dmr.ChildId, dmr.RequestedAt });
        });
    }
}
