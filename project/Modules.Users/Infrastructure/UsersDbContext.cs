using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Mediator;
using Modules.Users.Domain;
using Shared.Contracts.Companies.Events;
using System.Security.Claims;
using System.Text.Json;

namespace Modules.Users.Infrastructure;

internal sealed class UsersDbContext : IdentityDbContext<AppUser, AppRole, Guid>, IDataProtectionKeyContext
{
    private readonly IMediator _mediator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DbSet<AppRefreshToken> RefreshTokens { get; set; } = default!;
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = default!;
    public DbSet<Vehicle> Vehicles { get; set; } = default!;
    public DbSet<VehicleConnector> VehicleConnectors { get; set; } = default!;

    public UsersDbContext(
        DbContextOptions<UsersDbContext> options,
        IMediator mediator,
        IHttpContextAccessor httpContextAccessor)
        : base(options)
    {
        _mediator = mediator;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var auditEvents = await BuildAuditEventsAsync(cancellationToken);
        var result = await base.SaveChangesAsync(cancellationToken);
        foreach (var e in auditEvents)
        {
            await _mediator.Publish(e, cancellationToken);
        }

        return result;
    }

    public override int SaveChanges()
    {
        return SaveChangesAsync().GetAwaiter().GetResult();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Phase 1 compatibility mode: keep existing table mapping, defer schema split.
        builder.Entity<Vehicle>().ToTable("Vehicles");
        builder.Entity<VehicleConnector>().ToTable("VehicleConnectors");
        builder.Entity<AppRefreshToken>().ToTable("RefreshTokens");

        builder.Entity<Vehicle>()
            .HasMany(v => v.VehicleConnectors)
            .WithOne(vc => vc.Vehicle)
            .HasForeignKey(vc => vc.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<VehicleConnector>()
            .HasIndex(vc => vc.ConnectorId);

        builder.Entity<VehicleConnector>()
            .HasIndex(vc => new { vc.VehicleId, vc.ConnectorId });

        builder.Entity<AppRefreshToken>()
            .HasOne(rt => rt.User)
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private async Task<List<AuditLogMutationRequestedNotification>> BuildAuditEventsAsync(CancellationToken ct)
    {
        ChangeTracker.DetectChanges();

        var entries = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => e.Entity is Vehicle or VehicleConnector or AppRefreshToken)
            .ToList();

        if (entries.Count == 0)
        {
            return new List<AuditLogMutationRequestedNotification>();
        }

        var companyId = await ResolveRequestCompanyIdAsync(ct);
        if (!companyId.HasValue || companyId.Value == Guid.Empty)
        {
            // Users module entities are generally user-scoped, not tenant-scoped.
            // Persisted audit logs require a valid CompanyId, so unresolved scope is skipped.
            return new List<AuditLogMutationRequestedNotification>();
        }

        var actor = ResolveActorUserName();
        var events = new List<AuditLogMutationRequestedNotification>(entries.Count);
        foreach (var entry in entries)
        {
            var entityId = ResolveEntityId(entry);
            if (!entityId.HasValue || entityId.Value == Guid.Empty)
            {
                continue;
            }

            events.Add(new AuditLogMutationRequestedNotification
            {
                CompanyId = companyId.Value,
                UserName = actor,
                EntityName = entry.Entity.GetType().Name,
                EntityId = entityId.Value,
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

    private Task<Guid?> ResolveRequestCompanyIdAsync(CancellationToken ct)
    {
        var routeValues = _httpContextAccessor.HttpContext?.Request.RouteValues;
        if (routeValues != null && routeValues.TryGetValue("companyId", out var rawCompanyId)
            && rawCompanyId != null
            && Guid.TryParse(rawCompanyId.ToString(), out var companyIdFromRoute))
        {
            return Task.FromResult<Guid?>(companyIdFromRoute);
        }

        var queryCompanyId = _httpContextAccessor.HttpContext?.Request.Query["companyId"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(queryCompanyId) && Guid.TryParse(queryCompanyId, out var companyIdFromQuery))
        {
            return Task.FromResult<Guid?>(companyIdFromQuery);
        }

        if (_httpContextAccessor.HttpContext?.Items.TryGetValue("CompanyId", out var itemCompanyId) == true
            && itemCompanyId != null
            && Guid.TryParse(itemCompanyId.ToString(), out var companyIdFromItems))
        {
            return Task.FromResult<Guid?>(companyIdFromItems);
        }

        return Task.FromResult<Guid?>(null);
    }

    private string ResolveActorUserName()
    {
        var user = _httpContextAccessor.HttpContext?.User;
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
        if (entry.Entity is Vehicle vehicle) return vehicle.Id;
        if (entry.Entity is VehicleConnector connector) return connector.Id;
        if (entry.Entity is AppRefreshToken token) return token.Id;
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
}
