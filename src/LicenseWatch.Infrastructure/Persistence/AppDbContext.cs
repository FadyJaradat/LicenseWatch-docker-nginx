using LicenseWatch.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace LicenseWatch.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<LicenseWatch.Core.Entities.License> Licenses => Set<LicenseWatch.Core.Entities.License>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ImportSession> ImportSessions => Set<ImportSession>();
    public DbSet<ImportRow> ImportRows => Set<ImportRow>();
    public DbSet<ComplianceViolation> ComplianceViolations => Set<ComplianceViolation>();
    public DbSet<UsageDailySummary> UsageDailySummaries => Set<UsageDailySummary>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<JobExecutionLog> JobExecutionLogs => Set<JobExecutionLog>();
    public DbSet<ScheduledJobDefinition> ScheduledJobs => Set<ScheduledJobDefinition>();
    public DbSet<ReportPreset> ReportPresets => Set<ReportPreset>();
    public DbSet<OptimizationInsight> OptimizationInsights => Set<OptimizationInsight>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<EmailNotificationRule> EmailNotificationRules => Set<EmailNotificationRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<LicenseWatch.Core.Entities.License>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedAtUtc);
            entity.Property(e => e.Vendor).HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.Property(e => e.CostPerSeatMonthly).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(10);
            entity.Property(e => e.UseCustomThresholds).IsRequired();
            entity.HasOne(e => e.Category)
                .WithMany(c => c.Licenses)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.StoredFileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SizeBytes).IsRequired();
            entity.Property(e => e.UploadedByUserId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.UploadedAtUtc).IsRequired();
            entity.HasIndex(e => e.StoredFileName).IsUnique();
            entity.HasOne(e => e.License)
                .WithMany(l => l.Attachments)
                .HasForeignKey(e => e.LicenseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OccurredAtUtc).IsRequired();
            entity.Property(e => e.ActorUserId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ActorEmail).IsRequired().HasMaxLength(256);
            entity.Property(e => e.ActorDisplay).HasMaxLength(256);
            entity.Property(e => e.ImpersonatedUserId).HasMaxLength(100);
            entity.Property(e => e.ImpersonatedDisplay).HasMaxLength(256);
            entity.Property(e => e.CorrelationId).HasMaxLength(100);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EntityId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Summary).IsRequired().HasMaxLength(500);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.HasIndex(e => e.OccurredAtUtc);
            entity.HasIndex(e => e.Action);
            entity.HasIndex(e => e.EntityType);
            entity.HasIndex(e => e.CorrelationId);
        });

        modelBuilder.Entity<ImportSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.StoredFileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.CompletedAtUtc);
            entity.HasIndex(e => e.CreatedAtUtc);
            entity.HasMany(e => e.Rows)
                .WithOne(r => r.ImportSession)
                .HasForeignKey(r => r.ImportSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ImportRow>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RowNumber).IsRequired();
            entity.Property(e => e.LicenseIdRaw).HasMaxLength(50);
            entity.Property(e => e.LicenseName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CategoryName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Vendor).HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.IsValid).IsRequired();
            entity.HasIndex(e => e.ImportSessionId);
            entity.HasIndex(e => e.RowNumber);
        });

        modelBuilder.Entity<ComplianceViolation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RuleKey).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Severity).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Details).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.EvidenceJson).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.DetectedAtUtc).IsRequired();
            entity.Property(e => e.LastEvaluatedAtUtc).IsRequired();
            entity.Property(e => e.AcknowledgedByUserId).HasMaxLength(100);
            entity.HasIndex(e => new { e.Status, e.Severity });
            entity.HasIndex(e => new { e.LicenseId, e.Status });
            entity.HasIndex(e => new { e.LicenseId, e.RuleKey });
            entity.HasOne(e => e.License)
                .WithMany()
                .HasForeignKey(e => e.LicenseId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<UsageDailySummary>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UsageDateUtc).IsRequired();
            entity.Property(e => e.MaxSeatsUsed).IsRequired();
            entity.HasIndex(e => new { e.LicenseId, e.UsageDateUtc }).IsUnique();
            entity.HasOne(e => e.License)
                .WithMany()
                .HasForeignKey(e => e.LicenseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmailTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.SubjectTemplate).IsRequired().HasMaxLength(500);
            entity.Property(e => e.BodyHtmlTemplate).IsRequired();
            entity.Property(e => e.BodyTextTemplate).HasMaxLength(2000);
            entity.Property(e => e.IsEnabled).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedByUserId).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Key).IsUnique();
        });

        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.Type).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ToEmail).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Subject).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Error).HasMaxLength(1000);
            entity.Property(e => e.CorrelationId).HasMaxLength(100);
            entity.Property(e => e.TriggerEntityType).HasMaxLength(100);
            entity.Property(e => e.TriggerEntityId).HasMaxLength(100);
            entity.HasIndex(e => e.CreatedAtUtc);
            entity.HasIndex(e => new { e.Status, e.Type });
        });

        modelBuilder.Entity<EmailNotificationRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventKey).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Frequency).IsRequired().HasMaxLength(50);
            entity.Property(e => e.RoleRecipients).HasMaxLength(2000);
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedByUserId).HasMaxLength(100);
            entity.HasIndex(e => e.EventKey).IsUnique();
        });

        modelBuilder.Entity<JobExecutionLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.JobKey).IsRequired().HasMaxLength(100);
            entity.Property(e => e.StartedAtUtc).IsRequired();
            entity.Property(e => e.FinishedAtUtc);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(30);
            entity.Property(e => e.Summary).HasMaxLength(500);
            entity.Property(e => e.Error).HasMaxLength(1000);
            entity.Property(e => e.CorrelationId).HasMaxLength(100);
            entity.HasIndex(e => e.StartedAtUtc);
            entity.HasIndex(e => e.JobKey);
            entity.HasIndex(e => e.CorrelationId);
        });

        modelBuilder.Entity<ScheduledJobDefinition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.JobType).IsRequired().HasMaxLength(120);
            entity.Property(e => e.CronExpression).IsRequired().HasMaxLength(120);
            entity.Property(e => e.ParametersJson).HasMaxLength(4000);
            entity.Property(e => e.IsEnabled).IsRequired();
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
            entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.UpdatedByUserId).HasMaxLength(100);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.HasIndex(e => e.JobType);
        });

        modelBuilder.Entity<ReportPreset>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReportKey).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.FiltersJson).IsRequired().HasMaxLength(4000);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
            entity.Property(e => e.LastUsedAtUtc);
            entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.UpdatedByUserId).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.ReportKey);
            entity.HasIndex(e => new { e.ReportKey, e.Name }).IsUnique();
        });

        modelBuilder.Entity<OptimizationInsight>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Severity).IsRequired().HasMaxLength(20);
            entity.Property(e => e.DetectedAtUtc).IsRequired();
            entity.Property(e => e.EvidenceJson).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.IsActive).IsRequired();
            entity.HasIndex(e => e.Key);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.Severity);
            entity.HasOne(e => e.License)
                .WithMany()
                .HasForeignKey(e => e.LicenseId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Category)
                .WithMany()
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Recommendation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(10);
            entity.Property(e => e.EstimatedMonthlySavings).HasPrecision(18, 2);
            entity.Property(e => e.EstimatedAnnualSavings).HasPrecision(18, 2);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
            entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.UpdatedAtUtc);
            entity.HasOne(e => e.License)
                .WithMany()
                .HasForeignKey(e => e.LicenseId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.OptimizationInsight)
                .WithMany()
                .HasForeignKey(e => e.OptimizationInsightId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RoleName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PermissionKey).IsRequired().HasMaxLength(150);
            entity.Property(e => e.GrantedAtUtc).IsRequired();
            entity.Property(e => e.GrantedByUserId).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => new { e.RoleName, e.PermissionKey }).IsUnique();
            entity.HasIndex(e => e.RoleName);
        });
    }
}
