using App.BLL.DTOs;
using App.BLL.Mappers;
using App.BLL.Services.Interfaces;
using App.DAL.EF.Repositories.Interfaces;
using App.Domain;
using App.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace App.BLL.Services;

public class AdminPanelService : IAdminPanelService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<AppUser> _userManager;
    private readonly IAuditService _auditService;

    public AdminPanelService(
        IUnitOfWork unitOfWork,
        UserManager<AppUser> userManager,
        IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _auditService = auditService;
    }

    public async Task<ServiceResult<AdminDashboardDto>> GetDashboardAsync(DateTime fromUtc, DateTime toUtc)
    {
        if (fromUtc > toUtc)
        {
            return ServiceResult<AdminDashboardDto>.Fail("VALIDATION", "Invalid date range.");
        }

        var reservationsInPeriod = await _unitOfWork.Reservations.GetCountByRangeAsync(fromUtc, toUtc);
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
        var companies = (await _unitOfWork.Companies.GetAllIgnoringFiltersAsync()).ToList();
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            companies = companies
                .Where(company =>
                    (company.Name?.Translate() ?? string.Empty).Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    (company.Name?.Translate("et") ?? string.Empty).Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    company.Slug.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    company.ContactEmail.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var memberCounts = await _unitOfWork.AppUserCompanies.GetQueryable()
            .Where(uc => uc.IsActive)
            .GroupBy(uc => uc.CompanyId)
            .Select(group => new { CompanyId = group.Key, Count = group.Select(x => x.AppUserId).Distinct().Count() })
            .ToDictionaryAsync(item => item.CompanyId, item => item.Count);

        var mapped = companies
            .OrderBy(company => company.Name?.Translate() ?? string.Empty)
            .Select(company => BllDtoFactory.CreateAdminCompanyListItemDto(
                company,
                memberCounts.GetValueOrDefault(company.Id, 0)))
            .ToList();

        return ServiceResult<AdminCompanyListDto>.Ok(BllDtoFactory.CreateAdminCompanyListDto(normalizedSearch, mapped));
    }

    public async Task<ServiceResult<AdminStationListDto>> GetStationsAsync(string? search = null)
    {
        var stations = await _unitOfWork.ChargingStations.GetStationsWithConnectors()
            .Include(station => station.Company)
            .AsNoTracking()
            .ToListAsync();

        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            stations = stations
                .Where(station =>
                    (station.Name?.Translate() ?? string.Empty).Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    (station.Name?.Translate("et") ?? string.Empty).Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    station.Location.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    (station.Company?.Name?.Translate() ?? string.Empty).Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    (station.Company?.Name?.Translate("et") ?? string.Empty).Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var mapped = stations
            .OrderBy(station => station.Name?.Translate() ?? string.Empty)
            .ThenBy(station => station.Location)
            .Select(BllDtoFactory.CreateAdminStationListItemDto)
            .ToList();

        return ServiceResult<AdminStationListDto>.Ok(BllDtoFactory.CreateAdminStationListDto(normalizedSearch, mapped));
    }

    public async Task<ServiceResult<AdminCompanyListItemDto>> SetCompanyActivationAsync(Guid companyId, bool isActive, string actorUserName)
    {
        if (companyId == Guid.Empty)
        {
            return ServiceResult<AdminCompanyListItemDto>.Fail("VALIDATION", "Company id is required.");
        }

        var company = await _unitOfWork.Companies.GetByIdIgnoringFiltersAsync(companyId);
        if (company == null)
        {
            return ServiceResult<AdminCompanyListItemDto>.Fail("NOT_FOUND", "Company not found.");
        }

        if (company.IsActive != isActive)
        {
            company.IsActive = isActive;
            _unitOfWork.Companies.Update(company);
            await _unitOfWork.SaveAsync();
        }

        var memberCount = await _unitOfWork.AppUserCompanies.GetQueryable()
            .Where(uc => uc.CompanyId == companyId && uc.IsActive)
            .Select(uc => uc.AppUserId)
            .Distinct()
            .CountAsync();

        await _auditService.LogMutationAsync(
            company.Id,
            actorUserName,
            nameof(Company),
            company.Id,
            isActive ? "CompanyActivated" : "CompanyInactivated",
            $"{{\"isActive\":{isActive.ToString().ToLowerInvariant()}}}");

        return ServiceResult<AdminCompanyListItemDto>.Ok(BllDtoFactory.CreateAdminCompanyListItemDto(company, memberCount));
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
}
