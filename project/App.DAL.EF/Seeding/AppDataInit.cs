using System.Linq;
using App.Domain;
using App.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace App.DAL.EF.Seeding;

public static class AppDataInit
{
    private const string SeedCompanySlug = "seed-company";
    private const string SeedCompanyOwnerEmail = "owner@seed.com";

    public static void SeedAppData(AppDbContext context)
    {
        var seedOwner = context.Users.SingleOrDefault(u => u.Email == SeedCompanyOwnerEmail);
        if (seedOwner == null)
        {
            throw new ApplicationException($"Seed owner user '{SeedCompanyOwnerEmail}' was not found.");
        }

        var company = context.Companies.SingleOrDefault(c => c.Slug == SeedCompanySlug);
        if (company == null)
        {
            company = new Company
            {
                Id = Guid.NewGuid(),
                Name = new LangStr { ["en"] = "Seed Charging Company", ["et"] = "Näidis laadimisettevõte" },
                ContactEmail = "owner@seed.com",
                ContactPhone = "+3725000000",
                Slug = SeedCompanySlug,
                IsActive = true
            };

            context.Companies.Add(company);
            context.SaveChanges();
        }

        var membershipExists = context.AppUserCompanies.Any(uc =>
            uc.AppUserId == seedOwner.Id && uc.CompanyId == company.Id);
        if (!membershipExists)
        {
            context.AppUserCompanies.Add(new AppUserCompany
            {
                Id = Guid.NewGuid(),
                AppUserId = seedOwner.Id,
                CompanyId = company.Id,
                Role = ECompanyRole.Owner,
                IsActive = true,
                JoinedAtUtc = DateTime.UtcNow
            });
            context.SaveChanges();
        }

        var type2 = context.Connectors.AsEnumerable()
                        .SingleOrDefault(c => c.Name.ContainsKey("en") && c.Name["en"] == "Type 2 AC")
                    ?? new Connector
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "Type 2 AC", ["et"] = "Type 2 AC" },
            IsActive = true
        };

        var ccs = context.Connectors.AsEnumerable()
                      .SingleOrDefault(c => c.Name.ContainsKey("en") && c.Name["en"] == "CCS")
                  ?? new Connector
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "CCS", ["et"] = "CCS" },
            IsActive = true
        };

        var tesla = context.Connectors.AsEnumerable()
                        .SingleOrDefault(c => c.Name.ContainsKey("en") && c.Name["en"] == "Tesla")
                    ?? new Connector
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "Tesla", ["et"] = "Tesla" },
            IsActive = true
        };

        var chademo = context.Connectors.AsEnumerable()
                          .SingleOrDefault(c => c.Name.ContainsKey("en") && c.Name["en"] == "CHAdeMO")
                      ?? new Connector
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "CHAdeMO", ["et"] = "CHAdeMO" },
            IsActive = true
        };

        if (!context.Connectors.Any(c => c.Id == type2.Id)) context.Connectors.Add(type2);
        if (!context.Connectors.Any(c => c.Id == ccs.Id)) context.Connectors.Add(ccs);
        if (!context.Connectors.Any(c => c.Id == tesla.Id)) context.Connectors.Add(tesla);
        if (!context.Connectors.Any(c => c.Id == chademo.Id)) context.Connectors.Add(chademo);

        var downtownStation = context.ChargingStations.SingleOrDefault(s => s.Location == "Kesklinna tn 2");
        if (downtownStation == null)
        {
            downtownStation = new ChargingStation
            {
                Id = Guid.NewGuid(),
                Name = new LangStr { ["en"] = "Downtown Charging Hub", ["et"] = "Kesklinna laadimiskeskus" },
                Location = "Kesklinna tn 2",
                Status = EStationStatus.Available,
                PricePerKwh = 0.40m,
                MaxPower = 350m,
                IsActive = true,
                CompanyId = company.Id
            };
            context.ChargingStations.Add(downtownStation);
        }
        else if (downtownStation.CompanyId == null)
        {
            downtownStation.CompanyId = company.Id;
        }

        var northStation = context.ChargingStations.SingleOrDefault(s => s.Location == "Põhja tn 13");
        if (northStation == null)
        {
            northStation = new ChargingStation
            {
                Id = Guid.NewGuid(),
                Name = new LangStr { ["en"] = "North Side Charger", ["et"] = "Põhja laadija" },
                Location = "Põhja tn 13",
                Status = EStationStatus.InUse,
                PricePerKwh = 0.40m,
                MaxPower = 150m,
                IsActive = true,
                CompanyId = company.Id
            };
            context.ChargingStations.Add(northStation);
        }
        else if (northStation.CompanyId == null)
        {
            northStation.CompanyId = company.Id;
        }

        var airportStation = context.ChargingStations.SingleOrDefault(s => s.Location == "Lennujaama 42");
        if (airportStation == null)
        {
            airportStation = new ChargingStation
            {
                Id = Guid.NewGuid(),
                Name = new LangStr { ["en"] = "Airport Charging Point", ["et"] = "Lennujaama laadimispunkt" },
                Location = "Lennujaama 42",
                Status = EStationStatus.Maintenance,
                PricePerKwh = 0.40m,
                MaxPower = 50m,
                IsActive = true,
                CompanyId = company.Id
            };
            context.ChargingStations.Add(airportStation);
        }
        else if (airportStation.CompanyId == null)
        {
            airportStation.CompanyId = company.Id;
        }

        AddStationConnectorIfMissing(context, downtownStation.Id, type2.Id);
        AddStationConnectorIfMissing(context, downtownStation.Id, ccs.Id);
        AddStationConnectorIfMissing(context, downtownStation.Id, tesla.Id);
        AddStationConnectorIfMissing(context, northStation.Id, type2.Id);
        AddStationConnectorIfMissing(context, northStation.Id, ccs.Id);
        AddStationConnectorIfMissing(context, airportStation.Id, ccs.Id);
        AddStationConnectorIfMissing(context, airportStation.Id, tesla.Id);
        AddStationConnectorIfMissing(context, airportStation.Id, chademo.Id);

        context.SaveChanges();
    }

    private static void AddStationConnectorIfMissing(AppDbContext context, Guid stationId, Guid connectorId)
    {
        var exists = context.ChargingStationConnectors.Any(sc =>
            sc.ChargingStationId == stationId && sc.ConnectorId == connectorId);

        if (exists) return;

        context.ChargingStationConnectors.Add(new ChargingStationConnector
        {
            Id = Guid.NewGuid(),
            ChargingStationId = stationId,
            ConnectorId = connectorId
        });
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
