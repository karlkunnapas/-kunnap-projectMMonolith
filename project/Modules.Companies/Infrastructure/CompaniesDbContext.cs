using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Text.Json;
using Modules.Companies.Domain;
using Shared.Contracts;

namespace Modules.Companies.Infrastructure;

internal sealed class CompaniesDbContext : DbContext
{
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public CompaniesDbContext(DbContextOptions<CompaniesDbContext> options) : base(options)
    {
    }

    public CompaniesDbContext(DbContextOptions<CompaniesDbContext> options, IHttpContextAccessor httpContextAccessor) : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public DbSet<Company> Companies { get; set; } = default!;
    public DbSet<AppUserCompany> AppUserCompanies { get; set; } = default!;
    public DbSet<Promotion> Promotions { get; set; } = default!;
    public DbSet<UserPromotion> UserPromotions { get; set; } = default!;
    public DbSet<AuditLog> AuditLogs { get; set; } = default!;

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var auditEntries = await BuildAutomaticAuditEntriesAsync(cancellationToken);
        if (auditEntries.Count > 0)
        {
            AuditLogs.AddRange(auditEntries);
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        return SaveChangesAsync().GetAwaiter().GetResult();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Company>().ToTable("Companies");
        builder.Entity<AppUserCompany>().ToTable("AppUserCompanies");
        builder.Entity<Promotion>().ToTable("Promotions");
        builder.Entity<UserPromotion>().ToTable("UserPromotions");
        builder.Entity<AuditLog>().ToTable("AuditLogs");

        builder.Entity<Company>()
            .HasKey(x => x.Id);
        builder.Entity<AppUserCompany>()
            .HasKey(x => x.Id);
        builder.Entity<Promotion>()
            .HasKey(x => x.Id);
        builder.Entity<UserPromotion>()
            .HasKey(x => x.Id);
        builder.Entity<AuditLog>()
            .HasKey(x => x.Id);

        builder.Entity<Company>()
            .Property(x => x.Name)
            .HasColumnType("jsonb")
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                value => DeserializeLangStr(value));

        builder.Entity<Company>()
            .HasIndex(x => x.Slug)
            .IsUnique();

        builder.Entity<AppUserCompany>()
            .HasIndex(x => new { x.AppUserId, x.CompanyId })
            .IsUnique();

        builder.Entity<Promotion>()
            .HasIndex(x => x.Code);

        builder.Entity<Promotion>()
            .HasIndex(x => new { x.CompanyId, x.Code });

        builder.Entity<AuditLog>()
            .HasIndex(x => new { x.CompanyId, x.AtUtc });

        builder.Entity<AuditLog>()
            .HasIndex(x => new { x.CompanyId, x.EntityName, x.EntityId });

        DisableCascadeDeletes(builder);
    }

    private static void DisableCascadeDeletes(ModelBuilder builder)
    {
        foreach (var relationship in builder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    private async Task<List<AuditLog>> BuildAutomaticAuditEntriesAsync(CancellationToken ct)
    {
        ChangeTracker.DetectChanges();

        var entries = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => e.Entity is Company or AppUserCompany or Promotion or UserPromotion)
            .ToList();

        if (entries.Count == 0)
        {
            return new List<AuditLog>();
        }

        var actor = ResolveActorUserName();
        var result = new List<AuditLog>(entries.Count);

        foreach (var entry in entries)
        {
            var entityId = ResolveEntityId(entry);
            var companyId = await ResolveCompanyIdAsync(entry, ct);
            if (!entityId.HasValue || entityId.Value == Guid.Empty || !companyId.HasValue || companyId.Value == Guid.Empty)
            {
                continue;
            }

            result.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
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
                AtUtc = DateTime.UtcNow,
                ChangesJson = BuildChangesJson(entry)
            });
        }

        return result;
    }

    private async Task<Guid?> ResolveCompanyIdAsync(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, CancellationToken ct)
    {
        if (entry.Entity is Company company)
        {
            return company.Id;
        }

        if (entry.Entity is AppUserCompany membership)
        {
            return membership.CompanyId;
        }

        if (entry.Entity is Promotion promotion)
        {
            return promotion.CompanyId;
        }

        if (entry.Entity is UserPromotion userPromotion)
        {
            if (userPromotion.Promotion?.CompanyId.HasValue == true)
            {
                return userPromotion.Promotion.CompanyId.Value;
            }

            return await Promotions
                .AsNoTracking()
                .Where(p => p.Id == userPromotion.PromotionId)
                .Select(p => p.CompanyId)
                .FirstOrDefaultAsync(ct);
        }

        return null;
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
        if (entry.Entity is Company company) return company.Id;
        if (entry.Entity is AppUserCompany membership) return membership.Id;
        if (entry.Entity is Promotion promotion) return promotion.Id;
        if (entry.Entity is UserPromotion userPromotion) return userPromotion.Id;
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
}
