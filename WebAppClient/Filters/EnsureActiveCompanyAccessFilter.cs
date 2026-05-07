using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using WebAppClient.Models;
using WebAppClient.Services;

namespace WebAppClient.Filters;

public sealed class EnsureActiveCompanyAccessFilter(IApiClient apiClient) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!IsCompanyArea(context.ActionDescriptor) || IsAllowAnonymous(context))
        {
            await next();
            return;
        }

        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        try
        {
            var companies = await apiClient.GetAsync<UserCompaniesResponseDto>("api/v1/customeraccount/companies");
            if (companies.Companies.Count > 0)
            {
                await next();
                return;
            }

            var hasDeactivatedMembership = await apiClient.GetAsync<bool>("api/v1/customeraccount/has-deactivated-company-membership");
            context.Result = hasDeactivatedMembership
                ? new RedirectToActionResult("CompanyDeactivated", "Account", new { area = "" })
                : new ForbidResult();
        }
        catch (ApiException)
        {
            context.Result = new ForbidResult();
        }
    }

    private static bool IsCompanyArea(ActionDescriptor actionDescriptor)
    {
        if (!actionDescriptor.RouteValues.TryGetValue("area", out var area))
        {
            return false;
        }

        return string.Equals(area, "Company", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowAnonymous(FilterContext context)
    {
        return context.Filters.Any(filter => filter is IAllowAnonymousFilter) ||
               context.HttpContext.GetEndpoint()?.Metadata?.GetMetadata<IAllowAnonymous>() != null;
    }
}
