using App.DTO.v1.Company;
using App.DTO.v1.Identity;
using App.DTO.v1.Reservation;
using App.DTO.v1.Session;
using App.DTO.v1.Station;
using App.DTO.v1.Vehicle;
using ChargingStationContract = Shared.Contracts.Charging.ChargingStationContract;
using ChargingReservationContract = Shared.Contracts.Charging.ReservationContract;
using ChargingSessionContract = Shared.Contracts.Charging.ChargingSessionContract;
using CompanyPromotionContract = Shared.Contracts.Companies.CompanyPromotionContract;
using ConnectorContract = Shared.Contracts.Charging.ConnectorContract;
using UserPromotionContract = Shared.Contracts.Companies.UserPromotionContract;
using UserVehicleContract = Shared.Contracts.Users.UserVehicleContract;

namespace WebApp.Mappers;

public static class ApiDtoFactory
{
    public static JWTResponse CreateJwtResponse(string jwt, string refreshToken) => new()
    {
        JWT = jwt,
        RefreshToken = refreshToken
    };

    public static StationSummary CreateDto(ChargingStationContract dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        NameTranslations = dto.NameTranslations,
        Location = dto.Location,
        Status = dto.Status.ToString(),
        PricePerKwh = dto.PricePerKwh,
        MaxPower = dto.MaxPower,
        ConnectorNames = dto.Connectors.Select(c => c.Name).ToList(),
        IsCompatibleWithSelectedVehicle = null
    };

    public static ReservationResponse CreateDto(ChargingReservationContract dto) => new()
    {
        Id = dto.Id,
        StationId = dto.ChargingStationId,
        StationName = dto.StationName,
        StartTimeUtc = dto.StartTimeUtc,
        EndTimeUtc = dto.EndTimeUtc,
        ExpiresAtUtc = dto.ExpiresAtUtc,
        CancelledAtUtc = dto.CancelledAtUtc,
        Status = dto.Status.ToString(),
        EstimatedCost = dto.EstimatedCost
    };

    public static UserPromotionResponse CreateDto(UserPromotionContract dto) => new()
    {
        Id = dto.Id,
        PromotionId = dto.PromotionId,
        Code = dto.Promotion?.Code ?? string.Empty,
        DiscountValue = dto.Promotion?.DiscountValue ?? 0m,
        ValidFromUtc = dto.Promotion?.ValidFromUtc ?? default,
        ValidToUtc = dto.Promotion?.ValidToUtc ?? default,
        IsActive = dto.Promotion?.IsActive ?? false,
        IsUsed = dto.IsUsed
    };

    public static UserPromotionResponse CreateDto(CompanyPromotionContract dto, Guid userId) => new()
    {
        Id = Guid.Empty,
        PromotionId = dto.Id,
        Code = dto.Code,
        DiscountValue = dto.DiscountValue,
        ValidFromUtc = dto.ValidFromUtc,
        ValidToUtc = dto.ValidToUtc,
        IsActive = dto.IsActive,
        IsUsed = false
    };

    public static SessionResponse CreateDto(ChargingSessionContract dto)
    {
        var isActive = dto.EndTimeUtc == null;
        var durationMinutes = isActive
            ? Math.Max(1, (int)Math.Ceiling((DateTime.UtcNow - dto.StartTimeUtc).TotalMinutes))
            : Math.Max(1, (int)Math.Ceiling((dto.EndTimeUtc!.Value - dto.StartTimeUtc).TotalMinutes));

        var energyConsumedKwh = isActive
            ? CalculateEnergyEstimateKwh(durationMinutes, dto.StationMaxPower)
            : dto.EnergyConsumed;

        var calculatedCost = isActive
            ? Math.Round(dto.StationPricePerKwh * energyConsumedKwh, 2, MidpointRounding.AwayFromZero)
            : dto.Cost;

        var discountPct = dto.PromotionDiscountValue ?? 0m;
        var baseCost = !isActive && discountPct > 0m
            ? RecoverBaseCost(calculatedCost, discountPct)
            : calculatedCost;
        var finalCost = discountPct > 0m
            ? ApplyDiscount(baseCost, discountPct)
            : calculatedCost;
        var discountAmount = Math.Max(0m, baseCost - finalCost);

        return new SessionResponse
        {
            Id = dto.Id,
            StationId = dto.ChargingStationId,
            StationName = dto.StationName,
            ReservationId = dto.ReservationId,
            StartTimeUtc = dto.StartTimeUtc,
            EndTimeUtc = dto.EndTimeUtc,
            EnergyConsumedKwh = energyConsumedKwh,
            Cost = finalCost,
            BaseCostBeforeDiscount = baseCost,
            DiscountPercent = discountPct,
            DiscountAmount = discountAmount,
            PromotionCode = dto.PromotionCode,
            IsActive = isActive
        };
    }

    public static SessionDetailResponse CreateDetailDto(ChargingSessionContract dto)
    {
        var summary = CreateDto(dto);
        var duration = dto.EndTimeUtc.HasValue
            ? Math.Max(0, (int)Math.Round((dto.EndTimeUtc.Value - dto.StartTimeUtc).TotalMinutes))
            : Math.Max(0, (int)Math.Round((DateTime.UtcNow - dto.StartTimeUtc).TotalMinutes));

        return new SessionDetailResponse
        {
            Id = summary.Id,
            StationId = summary.StationId,
            StationName = summary.StationName,
            ReservationId = summary.ReservationId,
            StartTimeUtc = summary.StartTimeUtc,
            EndTimeUtc = summary.EndTimeUtc,
            EnergyConsumedKwh = summary.EnergyConsumedKwh,
            Cost = summary.Cost,
            BaseCostBeforeDiscount = summary.BaseCostBeforeDiscount,
            DiscountPercent = summary.DiscountPercent,
            DiscountAmount = summary.DiscountAmount,
            PromotionCode = summary.PromotionCode,
            IsActive = summary.IsActive,
            DurationMinutes = duration
        };
    }

    public static VehicleResponse CreateDto(UserVehicleContract dto, IReadOnlyDictionary<Guid, string> connectorNamesById) => new()
    {
        Id = dto.VehicleId,
        Make = dto.Make,
        Model = dto.Model,
        BatteryCapacity = dto.BatteryCapacity,
        CompatibleConnectors = dto.ConnectorIds
            .Select(connectorId => new VehicleConnectorResponse
            {
                ConnectorId = connectorId,
                Name = connectorNamesById.TryGetValue(connectorId, out var name) ? name : string.Empty
            })
            .ToList()
    };

    public static VehicleConnectorResponse CreateDto(ConnectorContract connector) => new()
    {
        ConnectorId = connector.Id,
        Name = connector.Name
    };

    public static ConnectorOption CreateDtoForStationOption(ConnectorContract connector) => new()
    {
        Id = connector.Id,
        Name = connector.Name
    };

    public static PromotionResponse CreateDto(CompanyPromotionContract dto) => new()
    {
        Id = dto.Id,
        Code = dto.Code,
        DiscountValue = dto.DiscountValue,
        ValidFromUtc = dto.ValidFromUtc,
        ValidToUtc = dto.ValidToUtc,
        IsActive = dto.IsActive
    };

    private static decimal CalculateEnergyEstimateKwh(int durationMinutes, decimal? stationMaxPower)
    {
        var effectivePower = Math.Max(1m, Math.Min(stationMaxPower ?? 50m, 200m));
        var durationHours = durationMinutes / 60m;
        return Math.Round(durationHours * effectivePower, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal ApplyDiscount(decimal baseCost, decimal discountPercent)
    {
        var safeDiscount = Math.Min(100m, Math.Max(0m, discountPercent));
        var discounted = baseCost * (1m - safeDiscount / 100m);
        return Math.Round(Math.Max(0m, discounted), 2, MidpointRounding.AwayFromZero);
    }

    private static decimal RecoverBaseCost(decimal discountedCost, decimal discountPercent)
    {
        var safeDiscount = Math.Min(100m, Math.Max(0m, discountPercent));
        if (safeDiscount <= 0m || safeDiscount >= 100m)
        {
            return discountedCost;
        }

        var baseCost = discountedCost / (1m - safeDiscount / 100m);
        return Math.Round(Math.Max(0m, baseCost), 2, MidpointRounding.AwayFromZero);
    }
}
