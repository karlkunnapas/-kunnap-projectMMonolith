using System.Security.Claims;
using App.DAL.EF;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace WebApp;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> ReservedFirstSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "Home",
        "Identity",
        "Admin",
        "System",
        "Company",
        "Account",
        "api",
        "css",
        "js",
        "lib",
        "_framework",
        "_content",
        "favicon.ico",
        "Vendor",
    };

    public async Task InvokeAsync(HttpContext context, AppDbContext db, ITenantContext tenantContext)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Redirect Identity Register to our custom Register page
        if (path.StartsWith("/Identity/Account/Register", StringComparison.OrdinalIgnoreCase))
        {
            var query = context.Request.QueryString.ToString();
            context.Response.Redirect($"/Account/Register{query}");
            return;
        }

        if (path.StartsWith("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase))
        {
            var query = context.Request.QueryString.ToString();
            context.Response.Redirect($"/Account/Login{query}");
            return;
        }

        // Set current user ID from claims if authenticated
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
        {
            tenantContext.SetCurrentUserId(userId);
        }

        // "/" -> no tenant
        if (path == "/" || string.IsNullOrWhiteSpace(path))
        {
            await next(context);
            return;
        }

        // Extract first segment: "/{first}/..."
        var first = path.TrimStart('/').Split('/', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(first))
        {
            await next(context);
            return;
        }

        if (ReservedFirstSegments.Contains(first))
        {
            await next(context);
            return;
        }

        // Resolve tenant by slug (ignore query filters so we can return a proper 404/disabled message)
        var company = await db.Companies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Slug == first);

        if (company is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            var notFound = new LangStr
            {
                ["en"] = "Company not found.",
                ["et"] = "Ettevotet ei leitud."
            };
            await context.Response.WriteAsync(notFound.Translate() ?? "Company not found.");
            return;
        }

        if (!company.IsActive)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            var inactive = new LangStr
            {
                ["en"] = "Company is deactivated.",
                ["et"] = "Ettevote on deaktiveeritud."
            };
            await context.Response.WriteAsync(inactive.Translate() ?? "Company is deactivated.");
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var currentUserId = tenantContext.CurrentUserId;
            if (currentUserId == null)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                var noUserContext = new LangStr
                {
                    ["en"] = "Access denied for this company.",
                    ["et"] = "Ligipaas sellele ettevottele on keelatud."
                };
                await context.Response.WriteAsync(noUserContext.Translate() ?? "Access denied for this company.");
                return;
            }

            var isSystemUser = context.User.IsInRole("root") || context.User.IsInRole("Admin");
            if (!isSystemUser)
            {
                var hasMembership = await db.AppUserCompanies
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .AnyAsync(uc => uc.AppUserId == currentUserId.Value && uc.CompanyId == company.Id && uc.IsActive);

                if (!hasMembership)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    var forbidden = new LangStr
                    {
                        ["en"] = "Access denied for this company.",
                        ["et"] = "Ligipaas sellele ettevottele on keelatud."
                    };
                    await context.Response.WriteAsync(forbidden.Translate() ?? "Access denied for this company.");
                    return;
                }
            }
        }

        tenantContext.SetCompany(company);

        await next(context);
    }
}