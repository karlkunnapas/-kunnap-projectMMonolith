using Microsoft.AspNetCore.Http;
using Moq;
using Shared.Contracts.Companies;
using Shared.Contracts.Tenancy;

namespace WebApp.Tests.Unit;

public class UnitTestTenantResolutionMiddleware
{
    [Fact]
    public async Task InvokeAsync_ReservedSegment_CallsNext()
    {
        var tenantContext = new TenantContext();
        var nextCalled = false;

        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new TenantResolutionMiddleware(next);
        var companiesModuleApi = new Mock<ICompaniesModuleApi>();
        var context = new DefaultHttpContext();
        context.Request.Path = "/Account/Register";

        await middleware.InvokeAsync(context, tenantContext, companiesModuleApi.Object);

        Assert.True(nextCalled);
        Assert.False(tenantContext.IsResolved);
    }

    [Fact]
    public async Task InvokeAsync_HomeSetLanguagePath_CallsNext_AndSkipsTenantLookup()
    {
        var tenantContext = new TenantContext();
        var nextCalled = false;

        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new TenantResolutionMiddleware(next);
        var companiesModuleApi = new Mock<ICompaniesModuleApi>();
        var context = new DefaultHttpContext();
        context.Request.Path = "/Home/SetLanguage";
        context.Request.QueryString = new QueryString("?culture=et&returnUrl=%2FAccount%2FRegister");

        await middleware.InvokeAsync(context, tenantContext, companiesModuleApi.Object);

        Assert.True(nextCalled);
        Assert.False(tenantContext.IsResolved);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_UnknownTenant_Returns404()
    {
        var tenantContext = new TenantContext();

        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);
        var companiesModuleApi = new Mock<ICompaniesModuleApi>();
        var context = new DefaultHttpContext();
        context.Request.Path = "/missing-tenant/festivaleditions";
        context.Response.Body = new MemoryStream();

        companiesModuleApi
            .Setup(api => api.GetCompanyTenantBySlugAsync("missing-tenant", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyTenantContract?)null);

        await middleware.InvokeAsync(context, tenantContext, companiesModuleApi.Object);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.False(tenantContext.IsResolved);
    }

    [Fact]
    public async Task InvokeAsync_InactiveTenant_Returns403()
    {
        var tenantContext = new TenantContext();
        var nextCalled = false;

        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };
        var middleware = new TenantResolutionMiddleware(next);
        var companiesModuleApi = new Mock<ICompaniesModuleApi>();
        var context = new DefaultHttpContext();
        context.Request.Path = "/inactive-tenant/festivaleditions";
        context.Response.Body = new MemoryStream();

        companiesModuleApi
            .Setup(api => api.GetCompanyTenantBySlugAsync("inactive-tenant", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyTenantContract
            {
                CompanyId = Guid.NewGuid(),
                Slug = "inactive-tenant",
                IsActive = false
            });

        await middleware.InvokeAsync(context, tenantContext, companiesModuleApi.Object);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.True(nextCalled);
        Assert.Equal("/Account/CompanyDeactivated", context.Request.Path);
        Assert.Equal("inactive-tenant", context.Items["DeactivatedCompanySlug"]?.ToString());
        Assert.False(tenantContext.IsResolved);
    }

    [Fact]
    public async Task InvokeAsync_ActiveTenant_SetsTenantContext_AndCallsNext()
    {
        var tenantContext = new TenantContext();
        var nextCalled = false;

        var companyId = Guid.NewGuid();

        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new TenantResolutionMiddleware(next);
        var companiesModuleApi = new Mock<ICompaniesModuleApi>();
        var context = new DefaultHttpContext();
        context.Request.Path = "/active-tenant/festivaleditions";

        companiesModuleApi
            .Setup(api => api.GetCompanyTenantBySlugAsync("active-tenant", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyTenantContract
            {
                CompanyId = companyId,
                Slug = "active-tenant",
                IsActive = true
            });

        await middleware.InvokeAsync(context, tenantContext, companiesModuleApi.Object);

        Assert.True(nextCalled);
        Assert.True(tenantContext.IsResolved);
        Assert.Equal(companyId, tenantContext.CompanyId);
        Assert.Equal("active-tenant", tenantContext.CompanySlug);
    }
}
