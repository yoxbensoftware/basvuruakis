using BasvuruAkis.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BasvuruAkis.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<AdminUserPermission> AdminUserPermissions => Set<AdminUserPermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<Neighborhood> Neighborhoods => Set<Neighborhood>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<RepresentativeOffice> RepresentativeOffices => Set<RepresentativeOffice>();
    public DbSet<AssignmentRule> AssignmentRules => Set<AssignmentRule>();
    public DbSet<ContentPage> ContentPages => Set<ContentPage>();
    public DbSet<LegalText> LegalTexts => Set<LegalText>();
    public DbSet<OtpRequest> OtpRequests => Set<OtpRequest>();
    public DbSet<ApplicationRecord> Applications => Set<ApplicationRecord>();
    public DbSet<ApplicationConsent> ApplicationConsents => Set<ApplicationConsent>();
    public DbSet<ApplicationAssignment> ApplicationAssignments => Set<ApplicationAssignment>();
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistories => Set<ApplicationStatusHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SecurityLog> SecurityLogs => Set<SecurityLog>();
    public DbSet<ExportLog> ExportLogs => Set<ExportLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.PasswordHash).HasMaxLength(512);
        });

        modelBuilder.Entity<AdminUserPermission>(entity =>
        {
            entity.HasKey(x => new { x.AdminUserId, x.Permission });
            entity.Property(x => x.Permission).HasMaxLength(128);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.Property(x => x.TokenHash).HasMaxLength(128);
        });

        modelBuilder.Entity<Province>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<District>().HasIndex(x => new { x.ProvinceId, x.Name }).IsUnique();
        modelBuilder.Entity<Neighborhood>().HasIndex(x => new { x.DistrictId, x.Name }).IsUnique();
        modelBuilder.Entity<RepresentativeOffice>().HasIndex(x => x.Name).IsUnique();

        modelBuilder.Entity<AssignmentRule>(entity =>
        {
            entity.HasIndex(x => new { x.Scope, x.ScopeId, x.Priority, x.IsActive });
            entity.Property(x => x.Scope).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<ContentPage>(entity =>
        {
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<LegalText>(entity =>
        {
            entity.HasIndex(x => new { x.Type, x.Version }).IsUnique();
            entity.HasIndex(x => new { x.Type, x.IsActive });
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<OtpRequest>(entity =>
        {
            entity.HasIndex(x => new { x.PhoneHash, x.CreatedAt });
            entity.HasIndex(x => x.VerificationTokenHash);
        });

        modelBuilder.Entity<ApplicationRecord>(entity =>
        {
            entity.HasIndex(x => x.ReferenceNumber).IsUnique();
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
            entity.HasIndex(x => x.NationalIdHash).IsUnique();
            entity.HasIndex(x => x.PhoneHash).IsUnique();
            entity.HasIndex(x => x.EmailHash);
            entity.HasIndex(x => new { x.ProvinceId, x.DistrictId, x.NeighborhoodId, x.CreatedAt });
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<ApplicationAssignment>(entity =>
        {
            entity.HasIndex(x => new { x.ApplicationId, x.CreatedAt });
        });

        modelBuilder.Entity<ApplicationStatusHistory>(entity =>
        {
            entity.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(x => new { x.Action, x.CreatedAt });
            entity.Property(x => x.Action).HasMaxLength(128);
        });

        modelBuilder.Entity<SecurityLog>(entity =>
        {
            entity.HasIndex(x => new { x.EventType, x.CreatedAt });
            entity.Property(x => x.EventType).HasMaxLength(128);
        });

        modelBuilder.Entity<ExportLog>(entity =>
        {
            entity.Property(x => x.Format).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        });

        var dateTimeOffsetConverter = new ValueConverter<DateTimeOffset, long>(
            value => value.ToUnixTimeMilliseconds(),
            value => DateTimeOffset.FromUnixTimeMilliseconds(value));

        var nullableDateTimeOffsetConverter = new ValueConverter<DateTimeOffset?, long?>(
            value => value.HasValue ? value.Value.ToUnixTimeMilliseconds() : null,
            value => value.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(value.Value) : null);

        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()))
        {
            if (property.ClrType == typeof(DateTimeOffset))
            {
                property.SetValueConverter(dateTimeOffsetConverter);
            }
            else if (property.ClrType == typeof(DateTimeOffset?))
            {
                property.SetValueConverter(nullableDateTimeOffsetConverter);
            }
        }
    }
}
