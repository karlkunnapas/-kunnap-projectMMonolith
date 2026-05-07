using System.Security.Claims;
using App.DAL.EF;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace WebApp.Filters;

public sealed class EnsureActiveCompanyAccessFilter(AppDbContext context) : IAsyncActionFilter
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

        var memberships = await context.AppUserCompanies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(uc => uc.Company)
            .Where(uc => uc.AppUserId == userId && uc.IsActive)
            .ToListAsync();

        if (memberships.Any(uc => uc.Company != null && uc.Company.IsActive))
        {
            await next();
            return;
        }

        if (memberships.Count > 0)
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
