using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Shared.Contracts.Companies;

namespace WebApp.Filters;

public sealed class EnsureActiveCompanyAccessFilter(ICompaniesModuleApi companiesModuleApi) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext contextAction, ActionExecutionDelegate next)
    {
        if (!IsCompanyArea(contextAction.ActionDescriptor) || IsAllowAnonymous(contextAction))
        {
            await next();
            return;
        }

        if (contextAction.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        var userIdValue = contextAction.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            contextAction.Result = new ForbidResult();
            return;
        }

        var memberships = await companiesModuleApi.GetUserCompaniesAsync(userId);
        if (memberships.Any(m =>
                string.Equals(m.Role, "Owner", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Role, "Manager", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Role, "Employee", StringComparison.OrdinalIgnoreCase)))
        {
            await next();
            return;
        }

        if (await companiesModuleApi.HasDeactivatedActiveMembershipAsync(userId))
        {
            contextAction.Result = new RedirectToActionResult("CompanyDeactivated", "Account", new { area = "" });
            return;
        }

        contextAction.Result = new ForbidResult();
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
