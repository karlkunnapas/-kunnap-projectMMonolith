using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Modules.Users.Domain;

namespace Modules.Users.Infrastructure;

internal sealed class UsersDbContext : IdentityDbContext<AppUser, AppRole, Guid>, IDataProtectionKeyContext
{
    public DbSet<AppRefreshToken> RefreshTokens { get; set; } = default!;
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = default!;
    public DbSet<Vehicle> Vehicles { get; set; } = default!;
    public DbSet<VehicleConnector> VehicleConnectors { get; set; } = default!;

    public UsersDbContext(DbContextOptions<UsersDbContext> options)
        : base(options)
    {
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
}
