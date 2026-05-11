using Microsoft.EntityFrameworkCore;
using Modules.Companies.Domain;

namespace Modules.Companies.Infrastructure;

internal sealed class CompaniesDbContext : DbContext
{
    public CompaniesDbContext(DbContextOptions<CompaniesDbContext> options) : base(options)
    {
    }

    public DbSet<Company> Companies { get; set; } = default!;
    public DbSet<AppUserCompany> AppUserCompanies { get; set; } = default!;
    public DbSet<Promotion> Promotions { get; set; } = default!;
    public DbSet<UserPromotion> UserPromotions { get; set; } = default!;
    public DbSet<AuditLog> AuditLogs { get; set; } = default!;

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
            .HasColumnType("jsonb");

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
    }
}
