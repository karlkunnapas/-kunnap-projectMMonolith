using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DAL.EF.Repositories.Interfaces;

namespace App.BLL.Services;

public class ChargingStationCompanyService : IChargingStationCompanyService
{
    private readonly IUnitOfWork _unitOfWork;

    public ChargingStationCompanyService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<List<CompanyStationDto>>> GetCompanyStationsAsync(Guid companyId)
    {
        if (companyId == Guid.Empty)
        {
            return ServiceResult<List<CompanyStationDto>>.Fail("VALIDATION", "Company id is required.");
        }

        var stations = await _unitOfWork.ChargingStations.GetByCompanyAsync(companyId);
        return ServiceResult<List<CompanyStationDto>>.Ok(stations.Select(MapStation).ToList());
    }

    public async Task<ServiceResult<CompanyStationDto>> GetStationDetailsAsync(Guid stationId, Guid companyId)
    {
        if (stationId == Guid.Empty || companyId == Guid.Empty)
        {
            return ServiceResult<CompanyStationDto>.Fail("VALIDATION", "Station id and company id are required.");
        }

        var station = await _unitOfWork.ChargingStations.GetByIdForCompanyAsync(stationId, companyId);
        if (station == null)
        {
            return ServiceResult<CompanyStationDto>.Fail("FORBIDDEN", "Charging station not found or access denied.");
        }

        return ServiceResult<CompanyStationDto>.Ok(MapStation(station));
    }

    private static CompanyStationDto MapStation(App.Domain.ChargingStation station)
    {
        return new CompanyStationDto
        {
            Id = station.Id,
            Name = station.Name.Translate() ?? station.Name.ToString() ?? string.Empty,
            Location = station.Location,
            Status = station.Status,
            PricePerKwh = station.PricePerKwh,
            MaxPower = station.MaxPower,
            IsActive = station.IsActive,
            Connectors = station.ChargingStationConnectors?
                .Where(link => link.Connector != null && link.Connector.IsActive)
                .Select(link => link.Connector!.Name.Translate() ?? link.Connector.Name.ToString() ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct()
                .OrderBy(value => value)
                .ToList() ?? new List<string>(),
            MaintenanceIssueCount = station.MaintenanceIssues?.Count(issue => issue.Status != App.Domain.EMaintenanceStatus.Resolved) ?? 0
        };
    }
}
