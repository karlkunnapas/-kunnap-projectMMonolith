using Microsoft.EntityFrameworkCore;
using Modules.Charging.Domain;

namespace Modules.Charging.Infrastructure;

internal sealed class ChargingDbContext : DbContext
{
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
            .Property(x => x.NameJson)
            .HasColumnName("Name")
            .HasColumnType("jsonb");

        builder.Entity<Connector>()
            .Property(x => x.NameJson)
            .HasColumnName("Name")
            .HasColumnType("jsonb");

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
    }
}
