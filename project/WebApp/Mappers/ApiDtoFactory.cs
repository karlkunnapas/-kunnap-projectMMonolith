using App.BLL.DTOs;
using App.Domain;
using App.DTO.v1.Company;
using App.DTO.v1.Identity;
using App.DTO.v1.Reservation;
using App.DTO.v1.Session;
using App.DTO.v1.Station;
using App.DTO.v1.Vehicle;
using ConnectorContract = Shared.Contracts.Charging.ConnectorContract;
using ChargingSessionContract = Shared.Contracts.Charging.ChargingSessionContract;
using ChargingReservationContract = Shared.Contracts.Charging.ReservationContract;
using CompanyPromotionContract = Shared.Contracts.Companies.CompanyPromotionContract;
using UserPromotionContract = Shared.Contracts.Companies.UserPromotionContract;
using UserVehicleContract = Shared.Contracts.Users.UserVehicleContract;

namespace WebApp.Mappers;

public static class ApiDtoFactory
{
    public static ReservationCreateDto CreateDto(ReservationCreate request)
    {
        return new ReservationCreateDto
        {
            StationId = request.StationId,
            StartTimeUtc = request.StartTimeUtc,
            EndTimeUtc = request.EndTimeUtc,
            EstimatedEnergyKwh = request.EstimatedEnergyKwh,
            PromotionCode = request.PromotionCode
        };
    }

    public static VehicleCreateDto CreateDto(VehicleCreate request)
    {
        return new VehicleCreateDto
        {
            Make = request.Make,
            Model = request.Model,
            BatteryCapacity = request.BatteryCapacity,
            ConnectorIds = request.ConnectorIds
        };
    }

    public static VehicleUpdateDto CreateDto(VehicleUpdate request)
    {
        return new VehicleUpdateDto
        {
            Make = request.Make,
            Model = request.Model,
            BatteryCapacity = request.BatteryCapacity,
            ConnectorIds = request.ConnectorIds
        };
    }

    public static ChargingSessionStartRequestDto CreateDto(SessionStartRequest request)
    {
        return new ChargingSessionStartRequestDto
        {
            StationId = request.StationId,
            ReservationId = request.ReservationId
        };
    }

    public static ChargingSessionStopRequestDto CreateDto(SessionStopRequest request)
    {
        return new ChargingSessionStopRequestDto
        {
            EnergyConsumedKwh = 0,
            DurationMinutes = null,
            PromotionCode = request.PromotionCode
        };
    }

    public static RegisterCustomerDto CreateDto(RegisterCustomer request)
    {
        return new RegisterCustomerDto
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            Password = request.Password,
            ConfirmPassword = request.ConfirmPassword
        };
    }

    public static RegisterCompanyOwnerDto CreateDto(RegisterCompanyOwner request)
    {
        return new RegisterCompanyOwnerDto
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            Password = request.Password,
            ConfirmPassword = request.ConfirmPassword,
            CompanyName = request.CompanyName,
            CompanySlug = request.CompanySlug
        };
    }

    public static PromotionUpsertDto CreateDto(PromotionUpsert request)
    {
        return new PromotionUpsertDto
        {
            Code = request.Code,
            DiscountValue = request.DiscountValue,
            ValidFromUtc = request.ValidFromUtc,
            ValidToUtc = request.ValidToUtc,
            IsActive = request.IsActive
        };
    }

    public static CompanyStationUpsertDto CreateDto(CompanyStationUpsert request)
    {
        return new CompanyStationUpsertDto
        {
            NameEn = request.NameEn,
            NameEt = request.NameEt,
            Location = request.Location,
            PricePerKwh = request.PricePerKwh,
            MaxPower = request.MaxPower,
            Status = (EStationStatus)request.Status,
            IsActive = request.IsActive,
            SelectedConnectorIds = request.SelectedConnectorIds
        };
    }

    public static AddCompanyUserRequestDto CreateDto(AddCompanyUserRequest request)
    {
        return new AddCompanyUserRequestDto
        {
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            Password = request.Password,
            ConfirmPassword = request.ConfirmPassword,
            Role = (ECompanyRole)request.Role
        };
    }

    public static UpdateCompanyUserRoleRequestDto CreateDto(UpdateCompanyUserRole request)
    {
        return new UpdateCompanyUserRoleRequestDto
        {
            Role = (ECompanyRole)request.Role
        };
    }

    public static JWTResponse CreateJwtResponse(string jwt, string refreshToken)
    {
        return new JWTResponse
        {
            JWT = jwt,
            RefreshToken = refreshToken
        };
    }

    public static UserCompaniesResponse CreateDto(UserCompanyListResultDto dto)
    {
        return new UserCompaniesResponse
        {
            UserId = dto.UserId,
            Email = dto.Email,
            Companies = dto.Companies.Select(CreateDto).ToList()
        };
    }

    public static UserCompanyItem CreateDto(CompanySelectionItemDto dto)
    {
        return new UserCompanyItem
        {
            MembershipId = dto.MembershipId,
            CompanyId = dto.CompanyId,
            CompanyName = dto.CompanyName,
            CompanySlug = dto.CompanySlug,
            Role = dto.Role
        };
    }

    public static StationSummary CreateDto(HomeStationDto dto)
    {
        return new StationSummary
        {
            Id = dto.Id,
            Name = dto.Name,
            NameTranslations = dto.NameTranslations,
            Location = dto.Location,
            Status = dto.Status.ToString(),
            PricePerKwh = dto.PricePerKwh,
            MaxPower = dto.MaxPower,
            ConnectorNames = dto.ConnectorNames,
            IsCompatibleWithSelectedVehicle = dto.IsCompatibleWithSelectedVehicle
        };
    }

    public static StationSummary CreateDto(Shared.Contracts.Charging.ChargingStationContract dto)
    {
        return new StationSummary
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
    }

    public static StationDetails CreateDto(StationDetailsDto dto)
    {
        return new StationDetails
        {
            Id = dto.Id,
            CompanyId = dto.CompanyId,
            Name = dto.Name,
            Location = dto.Location,
            Status = dto.Status.ToString(),
            PricePerKwh = dto.PricePerKwh,
            MaxPower = dto.MaxPower,
            Connectors = dto.Connectors.Select(CreateDto).ToList(),
            ExistingReservations = dto.ExistingReservations.Select(CreateDto).ToList(),
            AvailableSlots = dto.AvailableSlots.Select(CreateDto).ToList()
        };
    }

    public static ConnectorDetail CreateDto(ConnectorDetailDto dto)
    {
        return new ConnectorDetail
        {
            ConnectorId = dto.ConnectorId,
            Name = dto.Name,
            Quantity = dto.Quantity,
            AvailableQuantity = dto.AvailableQuantity,
            Reservations = dto.Reservations.Select(CreateDto).ToList()
        };
    }

    public static TimeRange CreateDto(ReservedTimeRangeDto dto)
    {
        return new TimeRange
        {
            StartTimeUtc = dto.StartTimeUtc,
            EndTimeUtc = dto.EndTimeUtc
        };
    }

    public static StationReservationSlot CreateDto(StationReservationDto dto)
    {
        return new StationReservationSlot
        {
            StartTimeUtc = dto.StartTimeUtc,
            EndTimeUtc = dto.EndTimeUtc,
            Status = dto.Status.ToString()
        };
    }

    public static AvailabilitySlot CreateDto(AvailabilitySlotDto dto)
    {
        return new AvailabilitySlot
        {
            StartTimeUtc = dto.StartTimeUtc,
            EndTimeUtc = dto.EndTimeUtc,
            IsAvailable = dto.IsAvailable
        };
    }

    public static CostEstimate CreateDto(CostEstimateDto dto)
    {
        return new CostEstimate
        {
            EstimatedCost = dto.EstimatedCost,
            DurationMinutes = dto.DurationMinutes
        };
    }

    public static ReservationResponse CreateDto(ReservationDto dto)
    {
        return new ReservationResponse
        {
            Id = dto.Id,
            StationId = dto.StationId,
            StationName = dto.StationName,
            StartTimeUtc = dto.StartTimeUtc,
            EndTimeUtc = dto.EndTimeUtc,
            ExpiresAtUtc = dto.ExpiresAtUtc,
            CancelledAtUtc = dto.CancelledAtUtc,
            Status = dto.Status.ToString(),
            EstimatedCost = dto.EstimatedCost
        };
    }

    public static ReservationResponse CreateDto(ChargingReservationContract dto)
    {
        return new ReservationResponse
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
    }

    public static UserPromotionResponse CreateDto(UserPromotionDto dto)
    {
        return new UserPromotionResponse
        {
            Id = dto.Id,
            PromotionId = dto.PromotionId,
            Code = dto.Code,
            DiscountValue = dto.DiscountValue,
            ValidFromUtc = dto.ValidFromUtc,
            ValidToUtc = dto.ValidToUtc,
            IsActive = dto.IsActive,
            IsUsed = dto.IsUsed
        };
    }

    public static UserPromotionResponse CreateDto(UserPromotionContract dto)
    {
        return new UserPromotionResponse
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
    }

    public static UserPromotionResponse CreateDto(CompanyPromotionContract dto, Guid userId)
    {
        return new UserPromotionResponse
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
    }

    public static SessionResponse CreateDto(ChargingSessionDto dto)
    {
        return new SessionResponse
        {
            Id = dto.Id,
            StationId = dto.StationId,
            StationName = dto.StationName,
            ReservationId = dto.ReservationId,
            StartTimeUtc = dto.StartTimeUtc,
            EndTimeUtc = dto.EndTimeUtc,
            EnergyConsumedKwh = dto.EnergyConsumedKwh,
            Cost = dto.Cost,
            BaseCostBeforeDiscount = dto.BaseCostBeforeDiscount,
            DiscountPercent = dto.DiscountPercent,
            DiscountAmount = dto.DiscountAmount,
            PromotionCode = dto.PromotionCode,
            IsActive = dto.IsActive
        };
    }

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

    public static SessionDetailResponse CreateDto(ChargingSessionDetailsDto dto)
    {
        return new SessionDetailResponse
        {
            Id = dto.Id,
            StationId = dto.StationId,
            StationName = dto.StationName,
            ReservationId = dto.ReservationId,
            StartTimeUtc = dto.StartTimeUtc,
            EndTimeUtc = dto.EndTimeUtc,
            DurationMinutes = dto.DurationMinutes,
            EnergyConsumedKwh = dto.EnergyConsumedKwh,
            Cost = dto.Cost,
            BaseCostBeforeDiscount = dto.BaseCostBeforeDiscount,
            DiscountPercent = dto.DiscountPercent,
            DiscountAmount = dto.DiscountAmount,
            PromotionCode = dto.PromotionCode,
            IsActive = dto.IsActive
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

    public static VehicleResponse CreateDto(VehicleDto dto)
    {
        return new VehicleResponse
        {
            Id = dto.Id,
            Make = dto.Make,
            Model = dto.Model,
            BatteryCapacity = dto.BatteryCapacity,
            CompatibleConnectors = dto.CompatibleConnectors.Select(CreateDto).ToList()
        };
    }

    public static VehicleResponse CreateDto(UserVehicleContract dto, IReadOnlyDictionary<Guid, string> connectorNamesById)
    {
        return new VehicleResponse
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
    }

    public static VehicleConnectorResponse CreateDto(VehicleConnectorDto dto)
    {
        return new VehicleConnectorResponse
        {
            ConnectorId = dto.ConnectorId,
            Name = dto.Name
        };
    }

    public static VehicleConnectorResponse CreateDto(Connector connector)
    {
        return new VehicleConnectorResponse
        {
            ConnectorId = connector.Id,
            Name = connector.Name.Translate() ?? connector.Name.ToString() ?? string.Empty
        };
    }

    public static VehicleConnectorResponse CreateDto(ConnectorContract connector)
    {
        return new VehicleConnectorResponse
        {
            ConnectorId = connector.Id,
            Name = connector.Name
        };
    }

    public static ConnectorOption CreateDtoForStationOption(Connector connector)
    {
        return new ConnectorOption
        {
            Id = connector.Id,
            Name = connector.Name.Translate() ?? connector.Name.ToString() ?? string.Empty
        };
    }

    public static ConnectorOption CreateDtoForStationOption(ConnectorContract connector)
    {
        return new ConnectorOption
        {
            Id = connector.Id,
            Name = connector.Name
        };
    }

    public static PromotionResponse CreateDto(PromotionSummaryDto dto)
    {
        return new PromotionResponse
        {
            Id = dto.Id,
            Code = dto.Code,
            DiscountValue = dto.DiscountValue,
            ValidFromUtc = dto.ValidFromUtc,
            ValidToUtc = dto.ValidToUtc,
            IsActive = dto.IsActive
        };
    }

    public static PromotionResponse CreateDto(CompanyPromotionContract dto)
    {
        return new PromotionResponse
        {
            Id = dto.Id,
            Code = dto.Code,
            DiscountValue = dto.DiscountValue,
            ValidFromUtc = dto.ValidFromUtc,
            ValidToUtc = dto.ValidToUtc,
            IsActive = dto.IsActive
        };
    }

    public static CompanyStationResponse CreateDto(CompanyStationDto dto)
    {
        return new CompanyStationResponse
        {
            Id = dto.Id,
            Name = dto.Name,
            Location = dto.Location,
            Status = dto.Status.ToString(),
            PricePerKwh = dto.PricePerKwh,
            MaxPower = dto.MaxPower,
            IsActive = dto.IsActive,
            Connectors = dto.Connectors,
            MaintenanceIssueCount = dto.MaintenanceIssueCount
        };
    }

    public static CompanyStationFormResponse CreateDto(CompanyStationFormDto dto)
    {
        return new CompanyStationFormResponse
        {
            Id = dto.Id,
            CompanyId = dto.CompanyId,
            NameEn = dto.NameEn,
            NameEt = dto.NameEt,
            Location = dto.Location,
            PricePerKwh = dto.PricePerKwh,
            MaxPower = dto.MaxPower,
            Status = dto.Status.ToString(),
            IsActive = dto.IsActive,
            SelectedConnectorIds = dto.SelectedConnectorIds,
            AvailableConnectors = dto.AvailableConnectors.Select(CreateDto).ToList()
        };
    }

    public static ConnectorAssignmentOption CreateDto(CompanyStationConnectorOptionDto dto)
    {
        return new ConnectorAssignmentOption
        {
            ConnectorId = dto.ConnectorId,
            ConnectorName = dto.ConnectorName,
            IsAssigned = dto.IsAssigned
        };
    }

    public static CompanyUserResponse CreateDto(CompanyUserMembershipDto dto)
    {
        return new CompanyUserResponse
        {
            MembershipId = dto.MembershipId,
            UserId = dto.UserId,
            Email = dto.Email,
            Role = dto.Role.ToString(),
            IsActive = dto.IsActive,
            JoinedAtUtc = dto.JoinedAtUtc
        };
    }

    public static AddCompanyUserResponse CreateDto(AddCompanyUserResultDto dto)
    {
        return new AddCompanyUserResponse
        {
            MembershipId = dto.MembershipId,
            UserId = dto.UserId,
            Email = dto.Email,
            Role = dto.Role.ToString(),
            IsExistingUser = dto.IsExistingUser,
            AccessStatus = dto.AccessStatus,
            NextAction = dto.NextAction
        };
    }

    public static MaintenanceIssueResponse CreateDto(MaintenanceIssueDto dto)
    {
        return new MaintenanceIssueResponse
        {
            Id = dto.Id,
            StationId = dto.StationId,
            StationName = dto.StationName,
            IssueDescription = dto.IssueDescription,
            Status = dto.Status.ToString(),
            ReportedAtUtc = dto.ReportedAtUtc,
            ResolvedAtUtc = dto.ResolvedAtUtc,
            AssignedToUserId = dto.AssignedToUserId,
            AssignedToUserName = dto.AssignedToUserName,
            ReporterUserName = dto.ReporterUserName,
            Notes = dto.Notes
        };
    }

    public static MaintenanceStatusHistoryResponse CreateDto(MaintenanceStatusHistoryDto dto)
    {
        return new MaintenanceStatusHistoryResponse
        {
            AtUtc = dto.AtUtc,
            Action = dto.Action,
            Actor = dto.Actor,
            Changes = dto.Changes
        };
    }

    public static DashboardResponse CreateDto(OperatorDashboardDto dto)
    {
        return new DashboardResponse
        {
            FromUtc = dto.FromUtc,
            ToUtc = dto.ToUtc,
            Kpis = CreateDto(dto.Kpis),
            StationStatus = dto.StationStatus.Select(CreateDto).ToList(),
            MaintenanceQueue = dto.MaintenanceQueue.Select(CreateDto).ToList(),
            UtilizationTrend = dto.UtilizationTrend.Select(CreateDto).ToList(),
            RevenueTrend = dto.RevenueTrend.Select(CreateDto).ToList()
        };
    }

    public static DashboardKpis CreateDto(OperatorKpiDto dto)
    {
        return new DashboardKpis
        {
            TotalStations = dto.TotalStations,
            TotalReservations = dto.TotalReservations,
            TotalSessions = dto.TotalSessions,
            TotalRevenue = dto.TotalRevenue,
            AvgUtilizationPercent = dto.AvgUtilizationPercent,
            AvgSessionDurationMinutes = dto.AvgSessionDurationMinutes,
            PeakHours = dto.PeakHours
        };
    }

    public static StationStatusCard CreateDto(CompanyStationStatusDto dto)
    {
        return new StationStatusCard
        {
            Id = dto.Id,
            Name = dto.Name,
            Status = dto.Status.ToString(),
            HealthStatus = dto.HealthStatus,
            UtilizationPercent = dto.UtilizationPercent,
            ActiveSessionsCount = dto.ActiveSessionsCount,
            ReservationsToday = dto.ReservationsToday,
            PendingMaintenanceCount = dto.PendingMaintenanceCount,
            RevenueToday = dto.RevenueToday
        };
    }

    public static ChartPoint CreateDto(ChartPointDto dto)
    {
        return new ChartPoint
        {
            Label = dto.Label,
            Value = dto.Value
        };
    }
}
