using App.BLL.Services;
using App.Domain;
using Moq;
using SC = Shared.Contracts.Charging;

namespace WebApp.Tests.Unit;

public class UnitTestOperatorDashboardService
{
    [Fact]
    public async Task GetStationStatusAsync_ReturnsOnlyCompanyStations_AndHealthStates()
    {
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();

        var stationGood = new SC.ChargingStationContract
        {
            Id = Guid.NewGuid(),
            Name = "Good station",
            Location = "A",
            Status = SC.EStationStatus.Available,
            PricePerKwh = 0.4m,
            MaxPower = 100m,
            CompanyId = companyA,
            IsActive = true,
            Connectors = new List<SC.ConnectorContract> { new() { Id = Guid.NewGuid(), Name = "CCS", IsActive = true } }
        };
        var stationCritical = new SC.ChargingStationContract
        {
            Id = Guid.NewGuid(),
            Name = "Critical station",
            Location = "B",
            Status = SC.EStationStatus.Maintenance,
            PricePerKwh = 0.4m,
            MaxPower = 100m,
            CompanyId = companyA,
            IsActive = true,
            Connectors = new List<SC.ConnectorContract> { new() { Id = Guid.NewGuid(), Name = "CCS", IsActive = true } }
        };
        var stationForeign = new SC.ChargingStationContract
        {
            Id = Guid.NewGuid(),
            Name = "Foreign station",
            Location = "C",
            Status = SC.EStationStatus.Available,
            PricePerKwh = 0.4m,
            MaxPower = 100m,
            CompanyId = companyB,
            IsActive = true,
            Connectors = new List<SC.ConnectorContract> { new() { Id = Guid.NewGuid(), Name = "CCS", IsActive = true } }
        };

        var mock = new Mock<SC.IChargingModuleApi>();
        mock.Setup(x => x.GetCompanyStationsAsync(companyA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SC.ChargingStationContract> { stationGood, stationCritical });
        mock.Setup(x => x.GetCompanyChargingSessionsAsync(companyA, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SC.ChargingSessionContract>());
        mock.Setup(x => x.GetCompanyReservationsAsync(companyA, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SC.ReservationContract>());
        mock.Setup(x => x.GetMaintenancesByCompanyAsync(companyA, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SC.MaintenanceContract>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyA,
                    ChargingStationId = stationCritical.Id,
                    StationName = stationCritical.Name,
                    IssueDescription = "Critical issue",
                    Status = SC.EMaintenanceStatus.InProgress,
                    ReportedAtUtc = DateTime.UtcNow.AddHours(-3)
                }
            });

        var service = new OperatorDashboardService(mock.Object);

        var result = await service.GetStationStatusAsync(companyA, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!.Count);
        Assert.DoesNotContain(result.Data, item => item.Name.Contains("Foreign", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Data, item => item.Name.Contains("Critical", StringComparison.OrdinalIgnoreCase) && item.HealthStatus == "Critical");
        Assert.Contains(result.Data, item => item.Name.Contains("Good", StringComparison.OrdinalIgnoreCase) && item.HealthStatus == "Good");
    }
}
