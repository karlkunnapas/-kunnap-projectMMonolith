using App.DAL.EF;
using Microsoft.AspNetCore.Http;

namespace WebApp.Helpers;

public class HttpContextAuditActorProvider(IHttpContextAccessor httpContextAccessor) : IAuditActorProvider
{
    public string? UserName => httpContextAccessor.HttpContext?.User?.Identity?.Name;
}

