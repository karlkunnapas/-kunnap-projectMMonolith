using App.BLL.DTOs;
using App.BLL.Mappers;
using App.BLL.Services.Interfaces;
using App.DAL.EF.Repositories.Interfaces;
using App.Domain;
using App.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Shared.Contracts.Charging;
using Shared.Contracts.Companies;

namespace App.BLL.Services;

public class AdminPanelService : IAdminPanelService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<AppUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly ICompaniesModuleApi _companiesModuleApi;
    private readonly IChargingModuleApi _chargingModuleApi;

    public AdminPanelService(
        IUnitOfWork unitOfWork,
        UserManager<AppUser> userManager,
        IAuditService auditService,
        ICompaniesModuleApi companiesModuleApi,
        IChargingModuleApi chargingModuleApi)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _auditService = auditService;
        _companiesModuleApi = companiesModuleApi;
        _chargingModuleApi = chargingModuleApi;
    }

    public async Task<ServiceResult<AdminDashboardDto>> GetDashboardAsync(DateTime fromUtc, DateTime toUtc)
    {
        if (fromUtc > toUtc)
        {
            return ServiceResult<AdminDashboardDto>.Fail("VALIDATION", "Invalid date range.");
        }

        var reservationsInPeriod = await _chargingModuleApi.GetReservationCountByRangeAsync(fromUtc, toUtc);
        var companies = (await _unitOfWork.Companies.GetAllIgnoringFiltersAsync()).ToList();
        var totalCompanies = companies.Count;

        var totalCompanyUsers = await _unitOfWork.AppUserCompanies.GetQueryable()
            .Where(uc => uc.IsActive)
            .Select(uc => uc.AppUserId)
            .Distinct()
            .CountAsync();

        var totalUsers = await _userManager.Users.AsNoTracking().CountAsync();
        var totalClientUsers = Math.Max(0, totalUsers - totalCompanyUsers);

        return ServiceResult<AdminDashboardDto>.Ok(BllDtoFactory.CreateAdminDashboardDto(
            reservationsInPeriod,
            totalCompanies,
            totalCompanyUsers,
            totalClientUsers,
            fromUtc,
            toUtc));
    }

    public async Task<ServiceResult<AdminCompanyListDto>> GetCompaniesAsync(string? search = null)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var companies = await _companiesModuleApi.GetCompaniesForAdminAsync(normalizedSearch);
        var mapped = companies
            .Select(company => new AdminCompanyListItemDto
            {
                CompanyId = company.CompanyId,
                Name = company.CompanyName,
                ContactEmail = company.ContactEmail,
                Slug = company.Slug,
                IsActive = company.IsActive,
                ActiveMemberCount = company.ActiveMembersCount
            })
            .ToList();

        return ServiceResult<AdminCompanyListDto>.Ok(BllDtoFactory.CreateAdminCompanyListDto(normalizedSearch, mapped));
    }

    public async Task<ServiceResult<AdminStationListDto>> GetStationsAsync(string? search = null)
    {
        var stations = await _chargingModuleApi.GetStationsForAdminAsync();
        var companies = await _companiesModuleApi.GetCompaniesForAdminAsync();
        var companyNames = companies.ToDictionary(c => c.CompanyId, c => c.CompanyName);
        var currentLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            stations = stations
                .Where(station =>
                    station.NameEn.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    station.NameEt.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    station.Location.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    (station.CompanyId.HasValue
                     && companyNames.TryGetValue(station.CompanyId.Value, out var companyName)
                     && companyName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        var mapped = stations
            .OrderBy(station => GetLocalizedStationName(station, currentLanguage))
            .ThenBy(station => station.Location)
            .Select(station => new AdminStationListItemDto
            {
                StationId = station.StationId,
                Name = GetLocalizedStationName(station, currentLanguage),
                Location = station.Location,
                CompanyName = station.CompanyId.HasValue && companyNames.TryGetValue(station.CompanyId.Value, out var companyName)
                    ? companyName
                    : "-",
                Status = station.Status.ToString(),
                IsActive = station.IsActive,
                PricePerKwh = station.PricePerKwh,
                MaxPower = station.MaxPower
            })
            .ToList();

        return ServiceResult<AdminStationListDto>.Ok(BllDtoFactory.CreateAdminStationListDto(normalizedSearch, mapped));
    }

    private static string GetLocalizedStationName(AdminChargingStationContract station, string currentLanguage)
    {
        if (currentLanguage == "et" && !string.IsNullOrWhiteSpace(station.NameEt))
        {
            return station.NameEt;
        }

        if (!string.IsNullOrWhiteSpace(station.NameEn))
        {
            return station.NameEn;
        }

        return station.NameEt;
    }

    public async Task<ServiceResult<AdminCompanyListItemDto>> SetCompanyActivationAsync(Guid companyId, bool isActive, string actorUserName)
    {
        if (companyId == Guid.Empty)
        {
            return ServiceResult<AdminCompanyListItemDto>.Fail("VALIDATION", "Company id is required.");
        }

        var company = await _companiesModuleApi.SetCompanyActivationAsync(companyId, isActive);
        if (company == null)
        {
            return ServiceResult<AdminCompanyListItemDto>.Fail("NOT_FOUND", "Company not found.");
        }

        await _auditService.LogMutationAsync(
            company.CompanyId,
            actorUserName,
            nameof(Company),
            company.CompanyId,
            isActive ? "CompanyActivated" : "CompanyInactivated",
            $"{{\"isActive\":{isActive.ToString().ToLowerInvariant()}}}");

        return ServiceResult<AdminCompanyListItemDto>.Ok(new AdminCompanyListItemDto
        {
            CompanyId = company.CompanyId,
            Name = company.CompanyName,
            ContactEmail = company.ContactEmail,
            Slug = company.Slug,
            IsActive = company.IsActive,
            ActiveMemberCount = company.ActiveMembersCount
        });
    }

    public async Task<ServiceResult<AdminAuditLogListDto>> GetAuditLogsAsync(AdminAuditLogFilterDto filter)
    {
        if (filter == null)
        {
            return ServiceResult<AdminAuditLogListDto>.Fail("VALIDATION", "Filter is required.");
        }

        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize <= 0 ? 50 : Math.Min(filter.PageSize, 200);

        if (filter.FromUtc.HasValue && filter.ToUtc.HasValue && filter.FromUtc > filter.ToUtc)
        {
            return ServiceResult<AdminAuditLogListDto>.Fail("VALIDATION", "Invalid date range.");
        }

        var logs = await _unitOfWork.AuditLogQueries.GetSystemAsync(
            filter.FromUtc,
            filter.ToUtc,
            string.IsNullOrWhiteSpace(filter.EntityName) ? null : filter.EntityName.Trim(),
            string.IsNullOrWhiteSpace(filter.Action) ? null : filter.Action.Trim(),
            string.IsNullOrWhiteSpace(filter.Actor) ? null : filter.Actor.Trim(),
            filter.EntityId,
            page,
            pageSize);

        var totalCount = await _unitOfWork.AuditLogQueries.GetSystemCountAsync(
            filter.FromUtc,
            filter.ToUtc,
            string.IsNullOrWhiteSpace(filter.EntityName) ? null : filter.EntityName.Trim(),
            string.IsNullOrWhiteSpace(filter.Action) ? null : filter.Action.Trim(),
            string.IsNullOrWhiteSpace(filter.Actor) ? null : filter.Actor.Trim(),
            filter.EntityId);

        var normalizedFilter = BllDtoFactory.CreateAdminAuditLogFilterDto(
            filter.FromUtc,
            filter.ToUtc,
            filter.EntityName,
            filter.Action,
            filter.Actor,
            filter.EntityId,
            page,
            pageSize);

        var items = logs.Select(BllDtoFactory.CreateAdminAuditLogListItemDto).ToList();
        return ServiceResult<AdminAuditLogListDto>.Ok(
            BllDtoFactory.CreateAdminAuditLogListDto(normalizedFilter, items, totalCount, page, pageSize));
    }

    public async Task<ServiceResult<AdminPromotionListDto>> GetSystemPromotionsAsync()
    {
        var allPromotions = await _unitOfWork.Promotions.GetAllWithCompanyAsync();
        var mapped = allPromotions
            .Select(BllDtoFactory.CreateAdminPromotionListItemDto)
            .OrderByDescending(p => p.IsSystemLevel)
            .ThenByDescending(p => p.ValidToUtc)
            .ToList();
        
        return ServiceResult<AdminPromotionListDto>.Ok(BllDtoFactory.CreateAdminPromotionListDto(mapped));
    }

    public async Task<ServiceResult<AdminPromotionFormDto>> GetSystemPromotionAsync(Guid promotionId)
    {
        if (promotionId == Guid.Empty)
        {
            return ServiceResult<AdminPromotionFormDto>.Fail("VALIDATION", "Promotion id is required.");
        }

        var promotion = await _unitOfWork.Promotions.GetByIdAsync(promotionId);
        if (promotion == null || promotion.CompanyId.HasValue)
        {
            return ServiceResult<AdminPromotionFormDto>.Fail("NOT_FOUND", "System promotion not found.");
        }

        return ServiceResult<AdminPromotionFormDto>.Ok(BllDtoFactory.CreateAdminPromotionFormDto(promotion));
    }

    public async Task<ServiceResult<AdminPromotionFormDto>> CreateSystemPromotionAsync(AdminPromotionFormDto dto, string actorUserName)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            return ServiceResult<AdminPromotionFormDto>.Fail("VALIDATION", "Promotion code is required.");
        }

        if (dto.DiscountValue <= 0)
        {
            return ServiceResult<AdminPromotionFormDto>.Fail("VALIDATION", "Discount value must be positive.");
        }

        if (dto.ValidFromUtc >= dto.ValidToUtc)
        {
            return ServiceResult<AdminPromotionFormDto>.Fail("VALIDATION", "Valid from date must be before valid to date.");
        }

        var promotion = new Promotion
        {
            Id = Guid.NewGuid(),
            Code = dto.Code.Trim().ToUpper(),
            DiscountValue = dto.DiscountValue,
            ValidFrom = dto.ValidFromUtc,
            ValidTo = dto.ValidToUtc,
            IsActive = dto.IsActive,
            CompanyId = null
        };

        await _unitOfWork.Promotions.AddAsync(promotion);
        await _unitOfWork.SaveAsync();

        await _auditService.LogMutationAsync(
            Guid.Empty,
            actorUserName,
            nameof(Promotion),
            promotion.Id,
            "Create",
            $"{{\"code\":\"{promotion.Code}\",\"discountValue\":{promotion.DiscountValue}}}");

        return ServiceResult<AdminPromotionFormDto>.Ok(BllDtoFactory.CreateAdminPromotionFormDto(promotion));
    }

    public async Task<ServiceResult<AdminPromotionFormDto>> UpdateSystemPromotionAsync(Guid promotionId, AdminPromotionFormDto dto, string actorUserName)
    {
        if (promotionId == Guid.Empty)
        {
            return ServiceResult<AdminPromotionFormDto>.Fail("VALIDATION", "Promotion id is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            return ServiceResult<AdminPromotionFormDto>.Fail("VALIDATION", "Promotion code is required.");
        }

        if (dto.DiscountValue <= 0)
        {
            return ServiceResult<AdminPromotionFormDto>.Fail("VALIDATION", "Discount value must be positive.");
        }

        if (dto.ValidFromUtc >= dto.ValidToUtc)
        {
            return ServiceResult<AdminPromotionFormDto>.Fail("VALIDATION", "Valid from date must be before valid to date.");
        }

        var promotion = await _unitOfWork.Promotions.GetByIdAsync(promotionId);
        if (promotion == null || promotion.CompanyId.HasValue)
        {
            return ServiceResult<AdminPromotionFormDto>.Fail("NOT_FOUND", "System promotion not found.");
        }

        promotion.Code = dto.Code.Trim().ToUpper();
        promotion.DiscountValue = dto.DiscountValue;
        promotion.ValidFrom = dto.ValidFromUtc;
        promotion.ValidTo = dto.ValidToUtc;
        promotion.IsActive = dto.IsActive;

        _unitOfWork.Promotions.Update(promotion);
        await _unitOfWork.SaveAsync();

        await _auditService.LogMutationAsync(
            Guid.Empty,
            actorUserName,
            nameof(Promotion),
            promotion.Id,
            "Update",
            $"{{\"code\":\"{promotion.Code}\",\"discountValue\":{promotion.DiscountValue}}}");

        return ServiceResult<AdminPromotionFormDto>.Ok(BllDtoFactory.CreateAdminPromotionFormDto(promotion));
    }

    public async Task<ServiceResult> DeleteSystemPromotionAsync(Guid promotionId, string actorUserName)
    {
        if (promotionId == Guid.Empty)
        {
            return ServiceResult.Fail("VALIDATION", "Promotion id is required.");
        }

        var promotion = await _unitOfWork.Promotions.GetByIdAsync(promotionId);
        if (promotion == null || promotion.CompanyId.HasValue)
        {
            return ServiceResult.Fail("NOT_FOUND", "System promotion not found.");
        }

        _unitOfWork.Promotions.Remove(promotion);
        await _unitOfWork.SaveAsync();

        await _auditService.LogMutationAsync(
            Guid.Empty,
            actorUserName,
            nameof(Promotion),
            promotion.Id,
            "Delete",
            null);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<AdminConnectorTypeListDto>> GetConnectorTypesAsync(string? search = null)
    {
        var connectors = (await _chargingModuleApi.GetConnectorsAsync(includeInactive: true)).ToList();
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            connectors = connectors
                .Where(connector =>
                    connector.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var mapped = connectors
            .OrderBy(connector => connector.Name)
            .Select(connector => new AdminConnectorTypeListItemDto
            {
                ConnectorTypeId = connector.Id,
                Name = connector.Name,
                IsActive = connector.IsActive
            })
            .ToList();

        return ServiceResult<AdminConnectorTypeListDto>.Ok(
            BllDtoFactory.CreateAdminConnectorTypeListDto(normalizedSearch, mapped));
    }

    public async Task<ServiceResult<AdminConnectorTypeFormDto>> GetConnectorTypeAsync(Guid connectorTypeId)
    {
        if (connectorTypeId == Guid.Empty)
        {
            return ServiceResult<AdminConnectorTypeFormDto>.Fail("VALIDATION", "Connector type id is required.");
        }

        var connector = await _chargingModuleApi.GetConnectorTypeByIdAsync(connectorTypeId);
        if (connector == null)
        {
            return ServiceResult<AdminConnectorTypeFormDto>.Fail("NOT_FOUND", "Connector type not found.");
        }

        return ServiceResult<AdminConnectorTypeFormDto>.Ok(new AdminConnectorTypeFormDto
        {
            ConnectorTypeId = connector.ConnectorTypeId,
            NameEn = connector.NameEn,
            NameEt = connector.NameEt,
            IsActive = connector.IsActive
        });
    }

    public async Task<ServiceResult<AdminConnectorTypeFormDto>> CreateConnectorTypeAsync(AdminConnectorTypeFormDto dto, string actorUserName)
    {
        if (string.IsNullOrWhiteSpace(dto.NameEn))
        {
            return ServiceResult<AdminConnectorTypeFormDto>.Fail("VALIDATION", "English name is required.");
        }

        var connector = await _chargingModuleApi.CreateConnectorTypeAsync(dto.NameEn.Trim(), dto.NameEt.Trim(), dto.IsActive);

        await _auditService.LogMutationAsync(
            Guid.Empty,
            actorUserName,
            nameof(Connector),
            connector.ConnectorTypeId,
            "Create",
            $"{{\"nameEn\":\"{connector.NameEn}\",\"nameEt\":\"{connector.NameEt}\",\"isActive\":{connector.IsActive.ToString().ToLowerInvariant()}}}");

        return ServiceResult<AdminConnectorTypeFormDto>.Ok(new AdminConnectorTypeFormDto
        {
            ConnectorTypeId = connector.ConnectorTypeId,
            NameEn = connector.NameEn,
            NameEt = connector.NameEt,
            IsActive = connector.IsActive
        });
    }

    public async Task<ServiceResult<AdminConnectorTypeFormDto>> UpdateConnectorTypeAsync(Guid connectorTypeId, AdminConnectorTypeFormDto dto, string actorUserName)
    {
        if (connectorTypeId == Guid.Empty)
        {
            return ServiceResult<AdminConnectorTypeFormDto>.Fail("VALIDATION", "Connector type id is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.NameEn))
        {
            return ServiceResult<AdminConnectorTypeFormDto>.Fail("VALIDATION", "English name is required.");
        }

        var connector = await _chargingModuleApi.UpdateConnectorTypeAsync(connectorTypeId, dto.NameEn.Trim(), dto.NameEt.Trim(), dto.IsActive);
        if (connector == null)
        {
            return ServiceResult<AdminConnectorTypeFormDto>.Fail("NOT_FOUND", "Connector type not found.");
        }

        await _auditService.LogMutationAsync(
            Guid.Empty,
            actorUserName,
            nameof(Connector),
            connector.ConnectorTypeId,
            "Update",
            $"{{\"nameEn\":\"{connector.NameEn}\",\"nameEt\":\"{connector.NameEt}\",\"isActive\":{connector.IsActive.ToString().ToLowerInvariant()}}}");

        return ServiceResult<AdminConnectorTypeFormDto>.Ok(new AdminConnectorTypeFormDto
        {
            ConnectorTypeId = connector.ConnectorTypeId,
            NameEn = connector.NameEn,
            NameEt = connector.NameEt,
            IsActive = connector.IsActive
        });
    }

    public async Task<ServiceResult> DeleteConnectorTypeAsync(Guid connectorTypeId, string actorUserName)
    {
        if (connectorTypeId == Guid.Empty)
        {
            return ServiceResult.Fail("VALIDATION", "Connector type id is required.");
        }

        var connector = await _chargingModuleApi.GetConnectorTypeByIdAsync(connectorTypeId);
        if (connector is null)
        {
            return ServiceResult.Fail("NOT_FOUND", "Connector type not found.");
        }

        await _chargingModuleApi.DeleteConnectorTypeAsync(connectorTypeId);

        await _auditService.LogMutationAsync(
            Guid.Empty,
            actorUserName,
            nameof(Connector),
            connector.ConnectorTypeId,
            "Delete",
            null);

        return ServiceResult.Ok();
    }
}
