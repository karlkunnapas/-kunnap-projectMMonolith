using System.Security.Claims;
using Shared.Contracts;
using Shared.Contracts.Companies;
using Shared.Contracts.Tenancy;

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
        "swagger",
        "root",
        "Station",
        "Reservation",
        "Users"
    };

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        ICompaniesModuleApi companiesModuleApi)
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

        // Resolve tenant by slug via Companies module contract.
        var company = await companiesModuleApi.GetCompanyTenantBySlugAsync(first);

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
            context.Request.Path = "/Account/CompanyDeactivated";
            context.Items["DeactivatedCompanySlug"] = first;
            await next(context);
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

            var isSystemUser = context.User.IsInRole("root") || context.User.IsInRole("Admin") || context.User.IsInRole("SystemAdmin");
            if (!isSystemUser)
            {
                var hasMembership = await companiesModuleApi.GetActiveCompanySelectionAsync(currentUserId.Value, company.CompanyId) != null;

                if (!hasMembership)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsync("Access denied for this company.");
                    return;
                }
            }
        }

        tenantContext.SetCompany(new TenantCompanyContext(
            company.CompanyId,
            company.Slug,
            company.IsActive));

        await next(context);
    }
}
