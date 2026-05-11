using App.DAL.EF;
using App.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shared.Contracts.Companies;

namespace WebApp.Tests.Unit;

public class UnitTestTenantResolutionMiddleware
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task InvokeAsync_ReservedSegment_CallsNext()
    {
        await using var db = CreateDbContext();
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

        await middleware.InvokeAsync(context, db, tenantContext, companiesModuleApi.Object);

        Assert.True(nextCalled);
        Assert.False(tenantContext.IsResolved);
    }

    [Fact]
    public async Task InvokeAsync_HomeSetLanguagePath_CallsNext_AndSkipsTenantLookup()
    {
        await using var db = CreateDbContext();
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

        await middleware.InvokeAsync(context, db, tenantContext, companiesModuleApi.Object);

        Assert.True(nextCalled);
        Assert.False(tenantContext.IsResolved);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_UnknownTenant_Returns404()
    {
        await using var db = CreateDbContext();
        var tenantContext = new TenantContext();

        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);
        var companiesModuleApi = new Mock<ICompaniesModuleApi>();
        var context = new DefaultHttpContext();
        context.Request.Path = "/missing-tenant/festivaleditions";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, db, tenantContext, companiesModuleApi.Object);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.False(tenantContext.IsResolved);
    }

    [Fact]
    public async Task InvokeAsync_InactiveTenant_Returns403()
    {
        await using var db = CreateDbContext();
        var tenantContext = new TenantContext();
        var nextCalled = false;

        db.Companies.Add(new Company
        {
            Name = new LangStr("Inactive"),
            ContactEmail = "inactive@example.com",
            ContactPhone = "+37255550001",
            Slug = "inactive-tenant",
            IsActive = false
        });
        await db.SaveChangesAsync();

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

        await middleware.InvokeAsync(context, db, tenantContext, companiesModuleApi.Object);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.True(nextCalled);
        Assert.Equal("/Account/CompanyDeactivated", context.Request.Path);
        Assert.Equal("inactive-tenant", context.Items["DeactivatedCompanySlug"]?.ToString());
        Assert.False(tenantContext.IsResolved);
    }

    [Fact]
    public async Task InvokeAsync_ActiveTenant_SetsTenantContext_AndCallsNext()
    {
        await using var db = CreateDbContext();
        var tenantContext = new TenantContext();
        var nextCalled = false;

        var company = new Company
        {
            Name = new LangStr("Active"),
            ContactEmail = "active@example.com",
            ContactPhone = "+37255550002",
            Slug = "active-tenant",
            IsActive = true
        };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new TenantResolutionMiddleware(next);
        var companiesModuleApi = new Mock<ICompaniesModuleApi>();
        var context = new DefaultHttpContext();
        context.Request.Path = "/active-tenant/festivaleditions";

        await middleware.InvokeAsync(context, db, tenantContext, companiesModuleApi.Object);

        Assert.True(nextCalled);
        Assert.True(tenantContext.IsResolved);
        Assert.Equal(company.Id, tenantContext.CompanyId);
        Assert.Equal(company.Slug, tenantContext.CompanySlug);
    }
}

