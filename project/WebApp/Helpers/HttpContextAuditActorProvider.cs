using Microsoft.AspNetCore.Http;
using Shared.Contracts.Auditing;

namespace WebApp.Helpers;

public class HttpContextAuditActorProvider(IHttpContextAccessor httpContextAccessor) : IAuditActorProvider
{
    public string? UserName => httpContextAccessor.HttpContext?.User?.Identity?.Name;
}
