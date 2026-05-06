using App.BLL.DTOs;
using App.Domain;
using App.Domain.Identity;

namespace App.BLL.Mappers;

public static class BllDtoFactory
{
    public static HomePageFilterDto CreateHomePageFilterDto(string? status, string? connector, string? location, Guid? vehicleId)
    {
        return new HomePageFilterDto
        {
            Status = status,
            Connector = connector,
            Location = location,
            VehicleId = vehicleId
        };
    }

    public static RegisterCustomerDto CreateRegisterCustomerDto(
        string firstName,
        string lastName,
        string email,
        string phoneNumber,
        string password,
        string confirmPassword)
    {
        return new RegisterCustomerDto
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PhoneNumber = phoneNumber,
            Password = password,
            ConfirmPassword = confirmPassword
        };
    }

    public static RegisterCompanyOwnerDto CreateRegisterCompanyOwnerDto(
        string firstName,
        string lastName,
        string email,
        string phoneNumber,
        string password,
        string confirmPassword,
        string companyName,
        string companySlug)
    {
        return new RegisterCompanyOwnerDto
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PhoneNumber = phoneNumber,
            Password = password,
            ConfirmPassword = confirmPassword,
            CompanyName = companyName,
            CompanySlug = companySlug
        };
    }

    public static AddCompanyUserRequestDto CreateAddCompanyUserRequestDto(
        string email,
        ECompanyRole role,
        string? firstName,
        string? lastName,
        string? phoneNumber,
        string? password,
        string? confirmPassword)
    {
        return new AddCompanyUserRequestDto
        {
            Email = email,
            Role = role,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber,
            Password = password,
            ConfirmPassword = confirmPassword
        };
    }

    public static UpdateCompanyUserRoleRequestDto CreateUpdateCompanyUserRoleRequestDto(ECompanyRole role)
    {
        return new UpdateCompanyUserRoleRequestDto
        {
            Role = role
        };
    }

    public static CompanyStationUpsertDto CreateCompanyStationUpsertDto(
        string nameEn,
        string nameEt,
        string location,
        decimal pricePerKwh,
        decimal maxPower,
        EStationStatus status,
        bool isActive,
        List<Guid> selectedConnectorIds)
    {
        return new CompanyStationUpsertDto
        {
            NameEn = nameEn,
            NameEt = nameEt,
            Location = location,
            PricePerKwh = pricePerKwh,
            MaxPower = maxPower,
            Status = status,
            IsActive = isActive,
            SelectedConnectorIds = selectedConnectorIds
        };
    }

    public static PromotionUpsertDto CreatePromotionUpsertDto(
        string code,
        decimal discountValue,
        DateTime validFromUtc,
        DateTime validToUtc,
        bool isActive)
    {
        return new PromotionUpsertDto
        {
            Code = code,
            DiscountValue = discountValue,
            ValidFromUtc = validFromUtc,
            ValidToUtc = validToUtc,
            IsActive = isActive
        };
    }

    public static ReservationCreateDto CreateReservationCreateDto(
        Guid stationId,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        decimal? estimatedEnergyKwh,
        string? promotionCode)
    {
        return new ReservationCreateDto
        {
            StationId = stationId,
            StartTimeUtc = startTimeUtc,
            EndTimeUtc = endTimeUtc,
            EstimatedEnergyKwh = estimatedEnergyKwh,
            PromotionCode = promotionCode
        };
    }

    public static VehicleCreateDto CreateVehicleCreateDto(
        string make,
        string model,
        decimal? batteryCapacity,
        List<Guid> connectorIds)
    {
        return new VehicleCreateDto
        {
            Make = make,
            Model = model,
            BatteryCapacity = batteryCapacity,
            ConnectorIds = connectorIds
        };
    }

    public static VehicleUpdateDto CreateVehicleUpdateDto(
        string make,
        string model,
        decimal? batteryCapacity,
        List<Guid> connectorIds)
    {
        return new VehicleUpdateDto
        {
            Make = make,
            Model = model,
            BatteryCapacity = batteryCapacity,
            ConnectorIds = connectorIds
        };
    }

    public static ChargingSessionStartRequestDto CreateChargingSessionStartRequestDto(Guid stationId, Guid? reservationId)
    {
        return new ChargingSessionStartRequestDto
        {
            StationId = stationId,
            ReservationId = reservationId
        };
    }

    public static ChargingSessionStopRequestDto CreateChargingSessionStopRequestDto(string? promotionCode)
    {
        return new ChargingSessionStopRequestDto
        {
            PromotionCode = promotionCode
        };
    }

    public static AvailabilitySlotDto CreateAvailabilitySlotDto(DateTime startTimeUtc, DateTime endTimeUtc, bool isAvailable)
    {
        return new AvailabilitySlotDto
        {
            StartTimeUtc = startTimeUtc,
            EndTimeUtc = endTimeUtc,
            IsAvailable = isAvailable
        };
    }

    public static ReservedTimeRangeDto CreateReservedTimeRangeDto(DateTime startTimeUtc, DateTime endTimeUtc)
    {
        return new ReservedTimeRangeDto
        {
            StartTimeUtc = startTimeUtc,
            EndTimeUtc = endTimeUtc
        };
    }

    public static StationReservationDto CreateStationReservationDto(DateTime startTimeUtc, DateTime endTimeUtc, EReservationStatus status)
    {
        return new StationReservationDto
        {
            StartTimeUtc = startTimeUtc,
            EndTimeUtc = endTimeUtc,
            Status = status
        };
    }

    public static ConnectorDetailDto CreateConnectorDetailDto(
        string name,
        int quantity,
        int availableQuantity,
        List<ReservedTimeRangeDto> reservations,
        Guid connectorId = default)
    {
        return new ConnectorDetailDto
        {
            ConnectorId = connectorId,
            Name = name,
            Quantity = quantity,
            AvailableQuantity = availableQuantity,
            Reservations = reservations
        };
    }

    public static StationDetailsDto CreateStationDetailsDto(
        ChargingStation station,
        List<ConnectorDetailDto> connectors,
        List<StationReservationDto> existingReservations,
        List<AvailabilitySlotDto> availableSlots)
    {
        return new StationDetailsDto
        {
            Id = station.Id,
            CompanyId = station.CompanyId,
            Name = station.Name.Translate() ?? station.Name.ToString() ?? string.Empty,
            Location = station.Location,
            Status = station.Status,
            PricePerKwh = station.PricePerKwh,
            MaxPower = station.MaxPower,
            Connectors = connectors,
            ExistingReservations = existingReservations,
            AvailableSlots = availableSlots
        };
    }

    public static ReservationDto CreateReservationDto(Reservation reservation, EReservationStatus status)
    {
        return new ReservationDto
        {
            Id = reservation.Id,
            StationId = reservation.ChargingStationId,
            StationName = reservation.ChargingStation?.Name.Translate() ?? reservation.ChargingStation?.Name.ToString() ?? string.Empty,
            StartTimeUtc = reservation.StartTime,
            EndTimeUtc = reservation.EndTime,
            ExpiresAtUtc = reservation.ExpiresAtUtc,
            CancelledAtUtc = reservation.CancelledAtUtc,
            EstimatedCost = reservation.EstimatedCost,
            Status = status
        };
    }

    public static CostEstimateDto CreateCostEstimateDto(int durationMinutes, decimal estimatedCost)
    {
        return new CostEstimateDto
        {
            DurationMinutes = durationMinutes,
            EstimatedCost = estimatedCost
        };
    }

    public static VehicleConnectorDto CreateVehicleConnectorDto(Guid connectorId, string name)
    {
        return new VehicleConnectorDto
        {
            ConnectorId = connectorId,
            Name = name
        };
    }

    public static VehicleDto CreateVehicleDto(Vehicle vehicle, List<VehicleConnectorDto> connectors)
    {
        return new VehicleDto
        {
            Id = vehicle.Id,
            Make = vehicle.Make,
            Model = vehicle.Model,
            BatteryCapacity = vehicle.BatteryCapacity,
            CompatibleConnectors = connectors
        };
    }

    public static CompatibleStationDto CreateCompatibleStationDto(ChargingStation station, List<string> compatibleConnectorNames)
    {
        return new CompatibleStationDto
        {
            StationId = station.Id,
            StationName = station.Name.Translate() ?? station.Name.ToString() ?? string.Empty,
            Location = station.Location,
            Status = station.Status,
            CompatibleConnectorNames = compatibleConnectorNames
        };
    }

    public static HomeStationDto CreateHomeStationDto(
        ChargingStation station,
        List<string> connectorNames,
        bool? isCompatibleWithSelectedVehicle,
        bool includeNameTranslations)
    {
        var dto = new HomeStationDto
        {
            Id = station.Id,
            Name = station.Name.Translate() ?? station.Name.ToString() ?? string.Empty,
            Location = station.Location,
            Status = station.Status,
            PricePerKwh = station.PricePerKwh,
            MaxPower = station.MaxPower,
            ConnectorNames = connectorNames,
            IsCompatibleWithSelectedVehicle = isCompatibleWithSelectedVehicle
        };

        if (includeNameTranslations)
        {
            dto.NameTranslations = new Dictionary<string, string>
            {
                ["en"] = station.Name?.Translate("en") ?? string.Empty,
                ["et"] = station.Name?.Translate("et") ?? string.Empty
            };
        }

        return dto;
    }

    public static HomePageDto CreateHomePageDto(IReadOnlyList<HomeStationDto> stations, IReadOnlyList<string> connectorFilters)
    {
        return new HomePageDto
        {
            Stations = stations,
            ConnectorFilters = connectorFilters
        };
    }

    public static OperatorKpiDto CreateOperatorKpiDto(
        int totalStations,
        int totalReservations,
        int totalSessions,
        decimal totalRevenue,
        decimal avgUtilizationPercent,
        double avgSessionDurationMinutes,
        string peakHours)
    {
        return new OperatorKpiDto
        {
            TotalStations = totalStations,
            TotalReservations = totalReservations,
            TotalSessions = totalSessions,
            TotalRevenue = totalRevenue,
            AvgUtilizationPercent = avgUtilizationPercent,
            AvgSessionDurationMinutes = avgSessionDurationMinutes,
            PeakHours = peakHours
        };
    }

    public static OperatorDashboardDto CreateOperatorDashboardDto(
        OperatorKpiDto kpis,
        List<CompanyStationStatusDto> stationStatus,
        List<MaintenanceIssueDto> maintenanceQueue,
        List<ChartPointDto> utilizationTrend,
        List<ChartPointDto> revenueTrend,
        DateTime fromUtc,
        DateTime toUtc)
    {
        return new OperatorDashboardDto
        {
            Kpis = kpis,
            StationStatus = stationStatus,
            MaintenanceQueue = maintenanceQueue,
            UtilizationTrend = utilizationTrend,
            RevenueTrend = revenueTrend,
            FromUtc = fromUtc,
            ToUtc = toUtc
        };
    }

    public static CompanyStationStatusDto CreateCompanyStationStatusDto(
        ChargingStation station,
        string healthStatus,
        decimal utilizationPercent,
        int activeSessionsCount,
        int reservationsToday,
        int pendingMaintenanceCount,
        decimal revenueToday)
    {
        return new CompanyStationStatusDto
        {
            Id = station.Id,
            Name = station.Name.Translate() ?? station.Name.ToString() ?? string.Empty,
            Status = station.Status,
            HealthStatus = healthStatus,
            UtilizationPercent = utilizationPercent,
            ActiveSessionsCount = activeSessionsCount,
            ReservationsToday = reservationsToday,
            PendingMaintenanceCount = pendingMaintenanceCount,
            RevenueToday = revenueToday
        };
    }

    public static ChartPointDto CreateChartPointDto(string label, decimal value)
    {
        return new ChartPointDto
        {
            Label = label,
            Value = value
        };
    }

    public static MaintenanceIssueDto CreateMaintenanceIssueDto(Maintenance issue)
    {
        return new MaintenanceIssueDto
        {
            Id = issue.Id,
            StationId = issue.ChargingStationId,
            StationName = issue.ChargingStation?.Name.Translate() ?? issue.ChargingStation?.Name.ToString() ?? string.Empty,
            IssueDescription = issue.IssueDescription,
            Status = issue.Status,
            ReportedAtUtc = issue.ReportedAt,
            ResolvedAtUtc = issue.ResolvedAt,
            AssignedToUserId = issue.AssignedToUserId,
            AssignedToUserName = issue.AssignedToUser?.UserName ?? string.Empty,
            ReporterUserName = issue.ReportedByUser?.UserName ?? string.Empty,
            Notes = issue.Notes
        };
    }

    public static MaintenanceStatusHistoryDto CreateMaintenanceStatusHistoryDto(AuditLog entry)
    {
        return new MaintenanceStatusHistoryDto
        {
            AtUtc = entry.AtUtc,
            Action = entry.Action,
            Actor = entry.UserName,
            Changes = entry.ChangesJson ?? string.Empty
        };
    }

    public static PromotionSummaryDto CreatePromotionSummaryDto(Promotion promotion)
    {
        return new PromotionSummaryDto
        {
            Id = promotion.Id,
            Code = promotion.Code,
            DiscountValue = promotion.DiscountValue,
            ValidFromUtc = promotion.ValidFrom,
            ValidToUtc = promotion.ValidTo,
            IsActive = promotion.IsActive
        };
    }

    public static UserPromotionDto CreateUserPromotionDto(UserPromotion userPromotion, DateTime fallbackUtc)
    {
        return new UserPromotionDto
        {
            Id = userPromotion.Id,
            PromotionId = userPromotion.PromotionId,
            Code = userPromotion.Promotion?.Code ?? string.Empty,
            DiscountValue = userPromotion.Promotion?.DiscountValue ?? 0m,
            ValidFromUtc = userPromotion.Promotion?.ValidFrom ?? fallbackUtc,
            ValidToUtc = userPromotion.Promotion?.ValidTo ?? fallbackUtc,
            AddedAtUtc = userPromotion.AddedAt,
            IsActive = userPromotion.Promotion?.IsActive ?? false,
            IsUsed = userPromotion.IsUsed
        };
    }

    public static AppliedPromotionDto CreateAppliedPromotionDto(Guid promotionId, string code, decimal discountValue)
    {
        return new AppliedPromotionDto
        {
            PromotionId = promotionId,
            Code = code,
            DiscountValue = discountValue
        };
    }

    public static ChargingSessionDto CreateChargingSessionDto(
        ChargingSession session,
        decimal baseCostBeforeDiscount,
        decimal discountPercent,
        decimal discountAmount,
        string? promotionCode)
    {
        return new ChargingSessionDto
        {
            Id = session.Id,
            StationId = session.ChargingStationId,
            StationName = session.ChargingStation?.Name.Translate() ?? session.ChargingStation?.Name.ToString() ?? string.Empty,
            ReservationId = session.ReservationId,
            StartTimeUtc = session.StartTime,
            EndTimeUtc = session.EndTime,
            EnergyConsumedKwh = session.EnergyConsumed,
            Cost = session.Cost,
            BaseCostBeforeDiscount = baseCostBeforeDiscount,
            DiscountPercent = discountPercent,
            DiscountAmount = discountAmount,
            PromotionCode = promotionCode,
            IsActive = session.EndTime == null
        };
    }

    public static ChargingSessionDetailsDto CreateChargingSessionDetailsDto(
        ChargingSession session,
        int durationMinutes,
        decimal energyConsumedKwh,
        decimal cost,
        decimal baseCostBeforeDiscount,
        decimal discountPercent,
        decimal discountAmount,
        string? promotionCode)
    {
        return new ChargingSessionDetailsDto
        {
            Id = session.Id,
            StationId = session.ChargingStationId,
            StationName = session.ChargingStation?.Name.Translate() ?? session.ChargingStation?.Name.ToString() ?? string.Empty,
            ReservationId = session.ReservationId,
            StartTimeUtc = session.StartTime,
            EndTimeUtc = session.EndTime,
            DurationMinutes = durationMinutes,
            EnergyConsumedKwh = energyConsumedKwh,
            Cost = cost,
            BaseCostBeforeDiscount = baseCostBeforeDiscount,
            DiscountPercent = discountPercent,
            DiscountAmount = discountAmount,
            PromotionCode = promotionCode,
            IsActive = session.EndTime == null
        };
    }

    public static AuditEntryDto CreateAuditEntryDto(AuditLog entry)
    {
        return new AuditEntryDto
        {
            Id = entry.Id,
            Action = entry.Action,
            UserName = entry.UserName,
            EntityName = entry.EntityName,
            EntityId = entry.EntityId,
            AtUtc = entry.AtUtc,
            ChangesJson = entry.ChangesJson
        };
    }

    public static AuditTrailDto CreateAuditTrailDto(string entityName, Guid entityId, List<AuditEntryDto> entries)
    {
        return new AuditTrailDto
        {
            EntityName = entityName,
            EntityId = entityId,
            Entries = entries
        };
    }

    public static CompanyStationConnectorOptionDto CreateCompanyStationConnectorOptionDto(Connector connector, bool isAssigned)
    {
        return new CompanyStationConnectorOptionDto
        {
            ConnectorId = connector.Id,
            ConnectorName = connector.Name.Translate() ?? connector.Name.ToString() ?? string.Empty,
            IsAssigned = isAssigned
        };
    }

    public static CompanyStationFormDto CreateCompanyStationFormDto(
        Guid? id,
        Guid companyId,
        string nameEn,
        string nameEt,
        string location,
        decimal pricePerKwh,
        decimal maxPower,
        EStationStatus status,
        bool isActive,
        List<Guid> selectedConnectorIds,
        List<CompanyStationConnectorOptionDto> availableConnectors)
    {
        return new CompanyStationFormDto
        {
            Id = id,
            CompanyId = companyId,
            NameEn = nameEn,
            NameEt = nameEt,
            Location = location,
            PricePerKwh = pricePerKwh,
            MaxPower = maxPower,
            Status = status,
            IsActive = isActive,
            SelectedConnectorIds = selectedConnectorIds,
            AvailableConnectors = availableConnectors
        };
    }

    public static CompanyStationDto CreateCompanyStationDto(ChargingStation station)
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
            MaintenanceIssueCount = station.MaintenanceIssues?.Count(issue => issue.Status != EMaintenanceStatus.Resolved) ?? 0
        };
    }

    public static CompanySelectionItemDto CreateCompanySelectionItemDto(AppUserCompany membership, bool translatedName)
    {
        var companyName = translatedName
            ? membership.Company?.Name.Translate() ?? membership.Company?.Name.ToString() ?? string.Empty
            : membership.Company?.Name?.ToString() ?? "Unknown";
        return new CompanySelectionItemDto
        {
            MembershipId = membership.Id,
            CompanyId = membership.CompanyId,
            CompanyName = companyName,
            CompanySlug = membership.Company?.Slug ?? string.Empty,
            Role = membership.Role.ToString()
        };
    }

    public static UserCompanyListResultDto CreateUserCompanyListResultDto(Guid userId, string email, List<CompanySelectionItemDto> companies)
    {
        return new UserCompanyListResultDto
        {
            UserId = userId,
            Email = email,
            Companies = companies
        };
    }

    public static CompanyUserMembershipDto CreateCompanyUserMembershipDto(AppUserCompany membership)
    {
        return new CompanyUserMembershipDto
        {
            MembershipId = membership.Id,
            UserId = membership.AppUserId,
            Email = membership.AppUser?.Email ?? string.Empty,
            Role = membership.Role,
            IsActive = membership.IsActive,
            JoinedAtUtc = membership.JoinedAtUtc
        };
    }

    public static AddCompanyUserResultDto CreateAddCompanyUserResultDto(
        Guid companyId,
        Guid membershipId,
        Guid userId,
        string email,
        ECompanyRole role,
        bool isExistingUser,
        string accessStatus,
        string nextAction,
        bool membershipReactivated = false,
        bool membershipAlreadyActive = false)
    {
        return new AddCompanyUserResultDto
        {
            CompanyId = companyId,
            MembershipId = membershipId,
            UserId = userId,
            Email = email,
            Role = role,
            IsExistingUser = isExistingUser,
            AccessStatus = accessStatus,
            NextAction = nextAction,
            MembershipReactivated = membershipReactivated,
            MembershipAlreadyActive = membershipAlreadyActive
        };
    }

    public static AdminDashboardDto CreateAdminDashboardDto(
        int reservationsInPeriod,
        int totalCompanies,
        int totalCompanyUsers,
        int totalClientUsers,
        DateTime fromUtc,
        DateTime toUtc)
    {
        return new AdminDashboardDto
        {
            ReservationsInPeriod = reservationsInPeriod,
            TotalCompanies = totalCompanies,
            TotalCompanyUsers = totalCompanyUsers,
            TotalClientUsers = totalClientUsers,
            FromUtc = fromUtc,
            ToUtc = toUtc
        };
    }

    public static AdminCompanyListItemDto CreateAdminCompanyListItemDto(Company company, int activeMemberCount)
    {
        return new AdminCompanyListItemDto
        {
            CompanyId = company.Id,
            Name = company.Name?.Translate() ?? company.Name?.ToString() ?? string.Empty,
            ContactEmail = company.ContactEmail,
            ContactPhone = company.ContactPhone,
            Slug = company.Slug,
            IsActive = company.IsActive,
            ActiveMemberCount = activeMemberCount
        };
    }

    public static AdminCompanyListDto CreateAdminCompanyListDto(string? search, List<AdminCompanyListItemDto> items)
    {
        return new AdminCompanyListDto
        {
            Search = search,
            Items = items
        };
    }

    public static AdminStationListItemDto CreateAdminStationListItemDto(ChargingStation station)
    {
        return new AdminStationListItemDto
        {
            StationId = station.Id,
            Name = station.Name?.Translate() ?? station.Name?.ToString() ?? string.Empty,
            Location = station.Location,
            CompanyName = station.Company?.Name?.Translate() ?? station.Company?.Name?.ToString() ?? "-",
            Status = station.Status.ToString(),
            IsActive = station.IsActive,
            PricePerKwh = station.PricePerKwh,
            MaxPower = station.MaxPower
        };
    }

    public static AdminStationListDto CreateAdminStationListDto(string? search, List<AdminStationListItemDto> items)
    {
        return new AdminStationListDto
        {
            Search = search,
            Items = items
        };
    }

    public static AdminAuditLogFilterDto CreateAdminAuditLogFilterDto(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? entityName,
        string? action,
        string? actor,
        Guid? entityId,
        int page,
        int pageSize)
    {
        return new AdminAuditLogFilterDto
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            EntityName = string.IsNullOrWhiteSpace(entityName) ? null : entityName.Trim(),
            Action = string.IsNullOrWhiteSpace(action) ? null : action.Trim(),
            Actor = string.IsNullOrWhiteSpace(actor) ? null : actor.Trim(),
            EntityId = entityId,
            Page = page,
            PageSize = pageSize
        };
    }

    public static AdminAuditLogListItemDto CreateAdminAuditLogListItemDto(AuditLog entry)
    {
        return new AdminAuditLogListItemDto
        {
            Id = entry.Id,
            CompanyId = entry.CompanyId,
            UserName = entry.UserName,
            EntityName = entry.EntityName,
            EntityId = entry.EntityId,
            Action = entry.Action,
            AtUtc = entry.AtUtc,
            ChangesJson = entry.ChangesJson
        };
    }

    public static AdminAuditLogListDto CreateAdminAuditLogListDto(
        AdminAuditLogFilterDto filter,
        List<AdminAuditLogListItemDto> items,
        int totalCount,
        int page,
        int pageSize)
    {
        return new AdminAuditLogListDto
        {
            Filter = filter,
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
