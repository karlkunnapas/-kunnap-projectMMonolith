using App.Domain;
using App.Domain.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Linq;
using System.Text.Json;

namespace App.DAL.EF;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IAuditActorProvider? auditActorProvider = null)
    : IdentityDbContext<AppUser, AppRole, Guid>(options), IDataProtectionKeyContext
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly IAuditActorProvider? _auditActorProvider = auditActorProvider;

    public DbSet<AppRefreshToken> RefreshTokens { get; set; }
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

    public DbSet<ChargingStation> ChargingStations { get; set; }
    public DbSet<Reservation> Reservations { get; set; }
    public DbSet<ChargingSession> ChargingSessions { get; set; }
    public DbSet<Maintenance> Maintenances { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<Connector> Connectors { get; set; }
    public DbSet<Company> Companies { get; set; }
    public DbSet<Promotion> Promotions { get; set; }
    public DbSet<VehicleConnector> VehicleConnectors { get; set; }
    public DbSet<ChargingStationConnector> ChargingStationConnectors { get; set; }
    public DbSet<AppUserCompany> AppUserCompanies { get; set; }
    public DbSet<UserPromotion> UserPromotions { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Ignore<LangStr>();

        // Configure all DateTime properties to use UTC
        ConfigureDateTimeAsUtc(builder);

        ApplyLangStrConversions(builder);

        // disable cascade delete
        foreach (var relationship in builder.Model
                     .GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }

        builder.Entity<AuditLog>()
            .HasIndex(a => new { a.CompanyId, a.AtUtc });

        builder.Entity<AuditLog>()
            .HasIndex(a => new { a.CompanyId, a.EntityName, a.EntityId });

        builder.Entity<Company>()
            .HasIndex(c => c.Slug)
            .IsUnique();

        builder.Entity<AppUserCompany>()
            .HasIndex(uc => new { uc.AppUserId, uc.CompanyId })
            .IsUnique();
    }

    private static void ApplyLangStrConversions(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            var langStrProperties = clrType
                .GetProperties()
                .Where(p => p.PropertyType == typeof(LangStr))
                .ToList();

            if (langStrProperties.Count == 0)
            {
                continue;
            }

            foreach (var property in langStrProperties)
            {
                builder.Entity(clrType)
                    .Property(property.PropertyType, property.Name)
                    .HasConversion(new ValueConverter<LangStr, string>(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => string.IsNullOrWhiteSpace(v)
                            ? new LangStr()
                            : JsonSerializer.Deserialize<LangStr>(v, (JsonSerializerOptions?)null)!))
                    .HasColumnType("jsonb")
                    .Metadata.SetValueComparer(new ValueComparer<LangStr>(
                        (left, right) => JsonSerializer.Serialize(left, (JsonSerializerOptions?)null) ==
                                         JsonSerializer.Serialize(right, (JsonSerializerOptions?)null),
                        value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null).GetHashCode(),
                        value => JsonSerializer.Deserialize<LangStr>(
                            JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                            (JsonSerializerOptions?)null)!));
            }
        }
    }

    public override int SaveChanges()
    {
        return SaveChanges(true);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AddAuditLogs();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(true, cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        AddAuditLogs();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void AddAuditLogs()
    {
        ChangeTracker.DetectChanges();

        var entries = ChangeTracker
            .Entries()
            .Where(e =>
                e.Entity is not AuditLog
                && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (entries.Count == 0) return;

        var actorUserName = _auditActorProvider?.UserName;
        var atUtc = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            var companyId = ResolveCompanyId(entry);
            if (companyId == null)
            {
                continue;
            }

            var auditLog = new AuditLog
            {
                CompanyId = companyId.Value,
                UserName = string.IsNullOrWhiteSpace(actorUserName) ? "system" : actorUserName,
                EntityName = entry.Metadata.ClrType.Name,
                EntityId = ResolveEntityId(entry),
                Action = ResolveAction(entry.State),
                AtUtc = atUtc,
                ChangesJson = BuildChangesJson(entry)
            };

            AuditLogs.Add(auditLog);
        }
    }

    private static string ResolveAction(EntityState state)
    {
        return state switch
        {
            EntityState.Added => "Create",
            EntityState.Modified => "Update",
            EntityState.Deleted => "Delete",
            _ => "Unknown"
        };
    }

    private Guid? ResolveCompanyId(EntityEntry entry)
    {
        if (entry.Entity is Company company)
        {
            return company.Id;
        }

        if (entry.Entity is Reservation reservation)
        {
            return ResolveStationCompanyId(reservation.ChargingStationId);
        }

        if (entry.Entity is ChargingSession session)
        {
            return ResolveStationCompanyId(session.ChargingStationId);
        }

        var companyIdProperty = entry.Properties
            .FirstOrDefault(p => p.Metadata.Name == nameof(AuditLog.CompanyId));

        if (companyIdProperty == null)
        {
            return null;
        }

        var rawValue = entry.State == EntityState.Deleted
            ? companyIdProperty.OriginalValue
            : companyIdProperty.CurrentValue;

        return rawValue is Guid value && value != Guid.Empty ? value : null;
    }

    private Guid? ResolveStationCompanyId(Guid stationId)
    {
        if (stationId == Guid.Empty)
        {
            return null;
        }

        var trackedStation = ChangeTracker.Entries<ChargingStation>()
            .FirstOrDefault(e => e.Entity.Id == stationId)
            ?.Entity;

        if (trackedStation?.CompanyId is Guid trackedCompanyId && trackedCompanyId != Guid.Empty)
        {
            return trackedCompanyId;
        }

        return ChargingStations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.Id == stationId)
            .Select(s => s.CompanyId)
            .FirstOrDefault();
    }

    private static Guid ResolveEntityId(EntityEntry entry)
    {
        var idProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == nameof(BaseEntity.Id));
        if (idProperty?.CurrentValue is Guid id && id != Guid.Empty)
        {
            return id;
        }

        if (idProperty?.OriginalValue is Guid originalId && originalId != Guid.Empty)
        {
            return originalId;
        }

        return Guid.Empty;
    }

    private static string? BuildChangesJson(EntityEntry entry)
    {
        var changes = new List<Dictionary<string, object?>>();

        foreach (var property in entry.Properties)
        {
            if (property.Metadata.IsPrimaryKey())
            {
                continue;
            }

            var propertyName = property.Metadata.Name;

            switch (entry.State)
            {
                case EntityState.Added:
                    changes.Add(new Dictionary<string, object?>
                    {
                        ["property"] = propertyName,
                        ["old"] = null,
                        ["new"] = property.CurrentValue
                    });
                    break;

                case EntityState.Deleted:
                    changes.Add(new Dictionary<string, object?>
                    {
                        ["property"] = propertyName,
                        ["old"] = property.OriginalValue,
                        ["new"] = null
                    });
                    break;

                case EntityState.Modified:
                    if (!property.IsModified || Equals(property.OriginalValue, property.CurrentValue))
                    {
                        continue;
                    }

                    changes.Add(new Dictionary<string, object?>
                    {
                        ["property"] = propertyName,
                        ["old"] = property.OriginalValue,
                        ["new"] = property.CurrentValue
                    });
                    break;
            }
        }

        if (changes.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(changes, AuditJsonOptions);
    }
    
    /// <summary>
    /// Configures all DateTime and DateTime? properties to convert to UTC when saving to PostgreSQL.
    /// PostgreSQL's 'timestamp with time zone' type requires UTC values.
    /// </summary>
    private static void ConfigureDateTimeAsUtc(ModelBuilder builder)
    {
        // Value converter for DateTime
        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(v, DateTimeKind.Utc)
                : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        // Value converter for DateTime?
        var nullableDateTimeConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue
                ? (v.Value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)
                    : v.Value.ToUniversalTime())
                : v,
            v => v.HasValue
                ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)
                : v);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(dateTimeConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(nullableDateTimeConverter);
                }
            }
        }
    }

}
