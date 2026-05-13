using Modules.Charging.Application.DTO;
using Shared.Contracts.Charging;

namespace Modules.Charging.Application.Mappers;

internal static class ChargingContractMapper
{
    public static ChargingStationContract ToContract(ChargingStationDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        NameTranslations = dto.NameTranslations,
        Location = dto.Location,
        Status = dto.Status,
        PricePerKwh = dto.PricePerKwh,
        MaxPower = dto.MaxPower,
        IsActive = dto.IsActive,
        CompanyId = dto.CompanyId,
        Connectors = dto.Connectors.Select(ToContract).ToList()
    };

    public static AdminChargingStationContract ToContract(AdminChargingStationDto dto) => new()
    {
        StationId = dto.StationId,
        CompanyId = dto.CompanyId,
        NameEn = dto.NameEn,
        NameEt = dto.NameEt,
        Location = dto.Location,
        Status = dto.Status,
        IsActive = dto.IsActive,
        PricePerKwh = dto.PricePerKwh,
        MaxPower = dto.MaxPower
    };

    public static ConnectorContract ToContract(ConnectorDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        IsActive = dto.IsActive
    };

    public static ConnectorTypeContract ToContract(ConnectorTypeDto dto) => new()
    {
        ConnectorTypeId = dto.ConnectorTypeId,
        NameEn = dto.NameEn,
        NameEt = dto.NameEt,
        IsActive = dto.IsActive
    };

    public static ReservationContract ToContract(ReservationDto dto) => new()
    {
        Id = dto.Id,
        UserId = dto.UserId,
        ChargingStationId = dto.ChargingStationId,
        StartTimeUtc = dto.StartTimeUtc,
        EndTimeUtc = dto.EndTimeUtc,
        ExpiresAtUtc = dto.ExpiresAtUtc,
        CancelledAtUtc = dto.CancelledAtUtc,
        EstimatedCost = dto.EstimatedCost,
        Status = dto.Status,
        PromotionId = dto.PromotionId,
        StationName = dto.StationName ?? string.Empty
    };

    public static ChargingSessionContract ToContract(ChargingSessionDto dto) => new()
    {
        Id = dto.Id,
        UserId = dto.UserId,
        ChargingStationId = dto.ChargingStationId,
        ReservationId = dto.ReservationId,
        PromotionId = dto.PromotionId,
        StartTimeUtc = dto.StartTimeUtc,
        EndTimeUtc = dto.EndTimeUtc,
        EnergyConsumed = dto.EnergyConsumed,
        Cost = dto.Cost,
        StationName = dto.StationName ?? string.Empty,
        StationPricePerKwh = dto.StationPricePerKwh,
        StationMaxPower = dto.StationMaxPower,
        PromotionCode = dto.PromotionCode,
        PromotionDiscountValue = dto.PromotionDiscountValue
    };

    public static MaintenanceContract ToContract(MaintenanceDto dto) => new()
    {
        Id = dto.Id,
        CompanyId = dto.CompanyId,
        ChargingStationId = dto.ChargingStationId,
        StationName = dto.StationName ?? string.Empty,
        ReportedByUserId = dto.ReportedByUserId,
        IssueDescription = dto.IssueDescription,
        Status = dto.Status,
        ReportedAtUtc = dto.ReportedAtUtc,
        ResolvedAtUtc = dto.ResolvedAtUtc,
        AssignedToUserId = dto.AssignedToUserId,
        Notes = dto.Notes
    };

    public static CompanyDashboardStatsContract ToContract(CompanyDashboardStatsDto dto) => new()
    {
        TotalStations = dto.TotalStations,
        AvailableStations = dto.AvailableStations,
        InUseStations = dto.InUseStations,
        MaintenanceStations = dto.MaintenanceStations,
        ActiveReservations = dto.ActiveReservations,
        ActiveSessions = dto.ActiveSessions,
        RevenueTotal = dto.RevenueTotal
    };

    public static UpsertCompanyStationDto ToDto(UpsertCompanyStationContract contract) => new()
    {
        StationId = contract.StationId,
        CompanyId = contract.CompanyId,
        NameEn = contract.NameEn,
        NameEt = contract.NameEt,
        Location = contract.Location,
        Status = contract.Status,
        PricePerKwh = contract.PricePerKwh,
        MaxPower = contract.MaxPower,
        IsActive = contract.IsActive
    };

    public static ReservationDto ToDto(ReservationContract contract) => new()
    {
        Id = contract.Id,
        UserId = contract.UserId,
        ChargingStationId = contract.ChargingStationId,
        StartTimeUtc = contract.StartTimeUtc,
        EndTimeUtc = contract.EndTimeUtc,
        ExpiresAtUtc = contract.ExpiresAtUtc,
        CancelledAtUtc = contract.CancelledAtUtc,
        EstimatedCost = contract.EstimatedCost,
        Status = contract.Status,
        PromotionId = contract.PromotionId,
        StationName = contract.StationName
    };

    public static ChargingSessionDto ToDto(ChargingSessionContract contract) => new()
    {
        Id = contract.Id,
        UserId = contract.UserId,
        ChargingStationId = contract.ChargingStationId,
        ReservationId = contract.ReservationId,
        PromotionId = contract.PromotionId,
        StartTimeUtc = contract.StartTimeUtc,
        EndTimeUtc = contract.EndTimeUtc,
        EnergyConsumed = contract.EnergyConsumed,
        Cost = contract.Cost,
        StationName = contract.StationName,
        StationPricePerKwh = contract.StationPricePerKwh,
        StationMaxPower = contract.StationMaxPower,
        PromotionCode = contract.PromotionCode,
        PromotionDiscountValue = contract.PromotionDiscountValue
    };

    public static MaintenanceDto ToDto(MaintenanceContract contract) => new()
    {
        Id = contract.Id,
        CompanyId = contract.CompanyId,
        ChargingStationId = contract.ChargingStationId,
        StationName = contract.StationName,
        ReportedByUserId = contract.ReportedByUserId,
        IssueDescription = contract.IssueDescription,
        Status = contract.Status,
        ReportedAtUtc = contract.ReportedAtUtc,
        ResolvedAtUtc = contract.ResolvedAtUtc,
        AssignedToUserId = contract.AssignedToUserId,
        Notes = contract.Notes
    };
}
