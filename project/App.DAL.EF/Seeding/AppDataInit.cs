using System.Linq;
using App.Domain;
using App.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace App.DAL.EF.Seeding;

public static class AppDataInit
{
    public static void SeedAppData(AppDbContext context)
    {
        if (context.Connectors.Any() || context.ChargingStations.Any())
        {
            return;
        }

        var type2 = new Connector
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "Type 2 AC", ["et"] = "Type 2 AC" },
            IsActive = true
        };

        var ccs = new Connector
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "CCS", ["et"] = "CCS" },
            IsActive = true
        };

        var tesla = new Connector
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "Tesla", ["et"] = "Tesla" },
            IsActive = true
        };

        var chademo = new Connector
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "CHAdeMO", ["et"] = "CHAdeMO" },
            IsActive = true
        };

        context.Connectors.AddRange(type2, ccs, tesla, chademo);

        var downtownStation = new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "Downtown Charging Hub", ["et"] = "Kesklinna laadimiskeskus" },
            Location = "2.3 km away",
            Status = EStationStatus.Available,
            PricePerKwh = 0.40m,
            MaxPower = 350m,
            IsActive = true
        };

        var northStation = new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "North Side Charger", ["et"] = "Pohja laadija" },
            Location = "3.8 km away",
            Status = EStationStatus.InUse,
            PricePerKwh = 0.40m,
            MaxPower = 150m,
            IsActive = true
        };

        var airportStation = new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "Airport Charging Point", ["et"] = "Lennujaama laadimispunkt" },
            Location = "5.2 km away",
            Status = EStationStatus.Maintenance,
            PricePerKwh = 0.40m,
            MaxPower = 50m,
            IsActive = true
        };

        context.ChargingStations.AddRange(downtownStation, northStation, airportStation);

        context.ChargingStationConnectors.AddRange(
            new ChargingStationConnector
            {
                Id = Guid.NewGuid(),
                ChargingStationId = downtownStation.Id,
                ConnectorId = type2.Id
            },
            new ChargingStationConnector
            {
                Id = Guid.NewGuid(),
                ChargingStationId = downtownStation.Id,
                ConnectorId = ccs.Id
            },
            new ChargingStationConnector
            {
                Id = Guid.NewGuid(),
                ChargingStationId = downtownStation.Id,
                ConnectorId = tesla.Id
            },
            new ChargingStationConnector
            {
                Id = Guid.NewGuid(),
                ChargingStationId = northStation.Id,
                ConnectorId = type2.Id
            },
            new ChargingStationConnector
            {
                Id = Guid.NewGuid(),
                ChargingStationId = northStation.Id,
                ConnectorId = ccs.Id
            },
            new ChargingStationConnector
            {
                Id = Guid.NewGuid(),
                ChargingStationId = airportStation.Id,
                ConnectorId = ccs.Id
            },
            new ChargingStationConnector
            {
                Id = Guid.NewGuid(),
                ChargingStationId = airportStation.Id,
                ConnectorId = tesla.Id
            },
            new ChargingStationConnector
            {
                Id = Guid.NewGuid(),
                ChargingStationId = airportStation.Id,
                ConnectorId = chademo.Id
            }
        );

        context.SaveChanges();
    }


    public static void MigrateDatabase(AppDbContext context)
    {
        context.Database.Migrate();
    }

    public static void DeleteDatabase(AppDbContext context)
    {
        context.Database.EnsureDeleted();
    }

    public static void SeedIdentity(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager)
    {
        foreach (var (roleName, id) in InitialData.Roles)
        {
            var role = roleManager.FindByNameAsync(roleName).Result;

            if (role != null) continue;

            role = new AppRole()
            {
                Name = roleName,
            };

            var result = roleManager.CreateAsync(role).Result;
            if (!result.Succeeded)
            {
                throw new ApplicationException("Role creation failed!");
            }
        }


        foreach (var userInfo in InitialData.Users)
        {
            var user = userManager.FindByEmailAsync(userInfo.name).Result;
            if (user == null)
            {
                user = new AppUser()
                {
                    Email = userInfo.name,
                    UserName = userInfo.name,
                    EmailConfirmed = true
                };
                var result = userManager.CreateAsync(user, userInfo.password).Result;
                if (!result.Succeeded)
                {
                    var errorMessage = string.Join("; ", result.Errors.Select(e => e.Description));
                    throw new ApplicationException($"User creation failed: {errorMessage}");
                }
            }

            foreach (var role in userInfo.roles)
            {
                if (userManager.IsInRoleAsync(user, role).Result)
                {
                    Console.WriteLine($"User {user.UserName} already in role {role}");
                    continue;
                }

                var roleResult = userManager.AddToRoleAsync(user, role).Result;
                if (!roleResult.Succeeded)
                {
                    foreach (var error in roleResult.Errors)
                    {
                        Console.WriteLine(error.Description);
                    }
                }
                else
                {
                    Console.WriteLine($"User {user.UserName} added to role {role}");
                }
            }
        }
    }
}