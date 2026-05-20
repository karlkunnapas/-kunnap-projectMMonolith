using Microsoft.EntityFrameworkCore;
using Mediator;
using Microsoft.AspNetCore.Http;
using Modules.Charging.Domain;
using Shared.Contracts;
using Shared.Contracts.Companies.Events;
using System.Security.Claims;
using System.Text.Json;

namespace Modules.Charging.Infrastructure;

internal sealed class ChargingDbContext : DbContext
{
    private readonly IMediator? _mediator;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public DbSet<ChargingStation> ChargingStations { get; set; } = default!;
    public DbSet<Reservation> Reservations { get; set; } = default!;
    public DbSet<ChargingSession> ChargingSessions { get; set; } = default!;
    public DbSet<Maintenance> Maintenances { get; set; } = default!;
    public DbSet<Connector> Connectors { get; set; } = default!;
    public DbSet<ChargingStationConnector> ChargingStationConnectors { get; set; } = default!;

    public ChargingDbContext(DbContextOptions<ChargingDbContext> options)
        : base(options)
    {
    }

    public ChargingDbContext(
        DbContextOptions<ChargingDbContext> options,
        IMediator mediator,
        IHttpContextAccessor httpContextAccessor)
        : base(options)
    {
        _mediator = mediator;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var pendingAudits = _mediator is null
            ? new List<AuditEnvelope>()
            : await BuildAuditEventsAsync(cancellationToken);
        var result = await base.SaveChangesAsync(cancellationToken);
        await PublishAuditEventsAsync(pendingAudits, cancellationToken);
        return result;
    }

    public override int SaveChanges()
    {
        return SaveChangesAsync().GetAwaiter().GetResult();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Phase 3 compatibility mode: keep existing table mapping, defer schema split.
        builder.Entity<ChargingStation>().ToTable("ChargingStations");
        builder.Entity<Reservation>().ToTable("Reservations");
        builder.Entity<ChargingSession>().ToTable("ChargingSessions");
        builder.Entity<Maintenance>().ToTable("Maintenances");
        builder.Entity<Connector>().ToTable("Connectors");
        builder.Entity<ChargingStationConnector>().ToTable("ChargingStationConnectors");

        builder.Entity<ChargingStation>()
            .Property(x => x.Name)
            .HasColumnName("Name")
            .HasColumnType("jsonb")
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                value => DeserializeLangStr(value));

        builder.Entity<Connector>()
            .Property(x => x.Name)
            .HasColumnName("Name")
            .HasColumnType("jsonb")
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                value => DeserializeLangStr(value));

        builder.Entity<ChargingStation>()
            .HasMany(x => x.Reservations)
            .WithOne(x => x.ChargingStation)
            .HasForeignKey(x => x.ChargingStationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ChargingStation>()
            .HasMany(x => x.ChargingSessions)
            .WithOne(x => x.ChargingStation)
            .HasForeignKey(x => x.ChargingStationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ChargingStation>()
            .HasMany(x => x.MaintenanceIssues)
            .WithOne(x => x.ChargingStation)
            .HasForeignKey(x => x.ChargingStationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ChargingStation>()
            .HasMany(x => x.ChargingStationConnectors)
            .WithOne(x => x.ChargingStation)
            .HasForeignKey(x => x.ChargingStationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Connector>()
            .HasMany(x => x.ChargingStationConnectors)
            .WithOne(x => x.Connector)
            .HasForeignKey(x => x.ConnectorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ChargingSession>()
            .HasOne(x => x.Reservation)
            .WithMany()
            .HasForeignKey(x => x.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ChargingStation>().HasIndex(x => x.CompanyId);
        builder.Entity<ChargingStation>().HasIndex(x => x.Status);
        builder.Entity<Reservation>().HasIndex(x => x.UserId);
        builder.Entity<Reservation>().HasIndex(x => new { x.ChargingStationId, x.StartTime, x.EndTime });
        builder.Entity<ChargingSession>().HasIndex(x => x.UserId);
        builder.Entity<ChargingSession>().HasIndex(x => x.ChargingStationId);
        builder.Entity<Maintenance>().HasIndex(x => x.ChargingStationId);
        builder.Entity<Maintenance>().HasIndex(x => x.Status);

        DisableCascadeDeletes(builder);
    }

    private static void DisableCascadeDeletes(ModelBuilder builder)
    {
        foreach (var relationship in builder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    private async Task<List<AuditEnvelope>> BuildAuditEventsAsync(CancellationToken ct)
    {
        ChangeTracker.DetectChanges();

        var entries = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => e.Entity is ChargingStation or Reservation or ChargingSession or Maintenance or ChargingStationConnector)
            .ToList();

        if (entries.Count == 0)
        {
            return new List<AuditEnvelope>();
        }

        var stationCompanyMap = new Dictionary<Guid, Guid>();

        async Task<Guid?> ResolveCompanyIdAsync(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
        {
            if (entry.Entity is ChargingStation station)
            {
                return station.CompanyId;
            }

            Guid? stationId = entry.Entity switch
            {
                Reservation r => r.ChargingStationId,
                ChargingSession s => s.ChargingStationId,
                Maintenance m => m.ChargingStationId,
                ChargingStationConnector c => c.ChargingStationId,
                _ => null
            };

            if (!stationId.HasValue || stationId.Value == Guid.Empty)
            {
                return null;
            }

            if (stationCompanyMap.TryGetValue(stationId.Value, out var cached))
            {
                return cached;
            }

            var existing = await ChargingStations
                .AsNoTracking()
                .Where(s => s.Id == stationId.Value)
                .Select(s => new { s.Id, s.CompanyId })
                .FirstOrDefaultAsync(ct);
            if (existing is null)
            {
                return null;
            }

            if (!existing.CompanyId.HasValue || existing.CompanyId.Value == Guid.Empty)
            {
                return null;
            }

            stationCompanyMap[existing.Id] = existing.CompanyId.Value;
            return existing.CompanyId.Value;
        }

        var actor = ResolveActorUserName();
        var events = new List<AuditEnvelope>(entries.Count);

        foreach (var entry in entries)
        {
            var companyId = await ResolveCompanyIdAsync(entry);
            if (!companyId.HasValue || companyId.Value == Guid.Empty)
            {
                continue;
            }

            var entityId = ResolveEntityId(entry);
            if (!entityId.HasValue || entityId.Value == Guid.Empty)
            {
                continue;
            }

            events.Add(new AuditEnvelope
            {
                CompanyId = companyId.Value,
                UserName = actor,
                EntityId = entityId.Value,
                EntityName = entry.Entity.GetType().Name,
                Action = entry.State switch
                {
                    EntityState.Added => "Created",
                    EntityState.Modified => "Updated",
                    EntityState.Deleted => "Deleted",
                    _ => "Updated"
                },
                ChangesJson = BuildChangesJson(entry),
                AtUtc = DateTime.UtcNow
            });
        }

        return events;
    }

    private async Task PublishAuditEventsAsync(IEnumerable<AuditEnvelope> events, CancellationToken ct)
    {
        foreach (var audit in events)
        {
            if (_mediator is null)
            {
                return;
            }

            await _mediator.Publish(new AuditLogMutationRequestedNotification
            {
                CompanyId = audit.CompanyId,
                UserName = audit.UserName,
                EntityName = audit.EntityName,
                EntityId = audit.EntityId,
                Action = audit.Action,
                ChangesJson = audit.ChangesJson,
                AtUtc = audit.AtUtc
            }, ct);
        }
    }

    private string ResolveActorUserName()
    {
        var user = _httpContextAccessor?.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return "system";
        }

        var name = user.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Trim();
        }

        var email = user.FindFirst(ClaimTypes.Email)?.Value;
        if (!string.IsNullOrWhiteSpace(email))
        {
            return email.Trim();
        }

        var subject = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return string.IsNullOrWhiteSpace(subject) ? "system" : subject.Trim();
    }

    private static Guid? ResolveEntityId(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        if (entry.Entity is ChargingStation s) return s.Id;
        if (entry.Entity is Reservation r) return r.Id;
        if (entry.Entity is ChargingSession cs) return cs.Id;
        if (entry.Entity is Maintenance m) return m.Id;
        if (entry.Entity is ChargingStationConnector c) return c.Id;
        return null;
    }

    private static string? BuildChangesJson(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        try
        {
            if (entry.State == EntityState.Added)
            {
                var current = entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
                return JsonSerializer.Serialize(current);
            }

            if (entry.State == EntityState.Deleted)
            {
                var original = entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue);
                return JsonSerializer.Serialize(original);
            }

            var changed = entry.Properties
                .Where(p => p.IsModified)
                .ToDictionary(
                    p => p.Metadata.Name,
                    p => new { Old = p.OriginalValue, New = p.CurrentValue });
            return changed.Count == 0 ? null : JsonSerializer.Serialize(changed);
        }
        catch
        {
            return null;
        }
    }

    private static LangStr DeserializeLangStr(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new LangStr();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<LangStr>(value, (JsonSerializerOptions?)null);
            if (parsed is not null)
            {
                return parsed;
            }
        }
        catch (JsonException)
        {
            // Backward compatibility for legacy plain-string rows.
        }

        return new LangStr(value, "en");
    }
    private sealed class AuditEnvelope
    {
        public Guid CompanyId { get; init; }
        public string UserName { get; init; } = string.Empty;
        public string EntityName { get; init; } = string.Empty;
        public Guid EntityId { get; init; }
        public string Action { get; init; } = string.Empty;
        public string? ChangesJson { get; init; }
        public DateTime AtUtc { get; init; }
    }
}
