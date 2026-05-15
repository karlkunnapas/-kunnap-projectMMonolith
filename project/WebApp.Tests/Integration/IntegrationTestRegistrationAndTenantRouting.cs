using System.Net;
using AngleSharp.Html.Dom;
using App.DAL.EF;
using App.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Companies;
using Shared.Contracts.Users;
using WebApp.Tests.Helpers;

namespace WebApp.Tests.Integration;

[Collection("Database tests")]
public class IntegrationTestRegistrationAndTenantRouting : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public IntegrationTestRegistrationAndTenantRouting(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task IdentityRegister_Redirects_ToCustomerRegister()
    {
        var response = await _client.GetAsync("/Identity/Account/Register");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Register", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task IdentityLogin_Redirects_ToCustomerLogin()
    {
        var response = await _client.GetAsync("/Identity/Account/Login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task RegisterPages_AreSeparated_AndReachable()
    {
        var customerResponse = await _client.GetAsync("/Account/Register");
        var loginResponse = await _client.GetAsync("/Account/Login");
        var companyResponse = await _client.GetAsync("/Company/Account/Register");

        Assert.Equal(HttpStatusCode.OK, customerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, companyResponse.StatusCode);
    }

    [Fact]
    public async Task UnknownTenant_ReturnsLocalizedMessage_ByCulture()
    {
        var enResponse = await _client.GetAsync("/missing-tenant/festivaleditions?culture=en");
        var etResponse = await _client.GetAsync("/missing-tenant/festivaleditions?culture=et");

        Assert.Equal(HttpStatusCode.NotFound, enResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, etResponse.StatusCode);

        var enBody = await enResponse.Content.ReadAsStringAsync();
        var etBody = await etResponse.Content.ReadAsStringAsync();

        Assert.Equal("Company not found.", enBody);
        Assert.Equal("Ettevotet ei leitud.", etBody);
    }

    [Fact]
    public async Task Login_WithOnlyDeactivatedCompany_RedirectsToCompanyDeactivatedPage()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"owner.{suffix}@example.com";
        var slug = $"deactivated-{suffix}";

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var usersModuleApi = scope.ServiceProvider.GetRequiredService<IUsersModuleApi>();
            var companiesModuleApi = scope.ServiceProvider.GetRequiredService<ICompaniesModuleApi>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();

            if (!await roleManager.RoleExistsAsync("CompanyOwner"))
            {
                var roleResult = await roleManager.CreateAsync(new AppRole { Name = "CompanyOwner" });
                Assert.True(roleResult.Succeeded, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
            }

            var userRegistration = await usersModuleApi.RegisterCustomerAsync(new RegisterCustomerContract
            {
                FirstName = "Owner",
                LastName = "User",
                Email = email,
                PhoneNumber = "+3725000001",
                Password = "Test.123"
            });
            Assert.True(userRegistration.Success);

            var registerResult = await companiesModuleApi.CreateCompanyWithOwnerMembershipAsync(new CreateCompanyWithOwnerMembershipContract
            {
                OwnerUserId = userRegistration.UserId,
                ContactEmail = email,
                ContactPhone = "+3725000001",
                CompanyName = $"Company {suffix}",
                CompanySlug = slug
            });

            Assert.True(registerResult.Success);
            var company = await db.Companies.IgnoreQueryFilters().FirstAsync(c => c.Id == registerResult.CompanyId);
            company.IsActive = false;
            await db.SaveChangesAsync();
        }

        var getLogin = await _client.GetAsync("/Account/Login");
        var loginDoc = await HtmlHelpers.GetDocumentAsync(getLogin);
        var form = Assert.IsAssignableFrom<IHtmlFormElement>(loginDoc.QuerySelector("form"));

        var post = await _client.SendAsync(form, new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = "Test.123",
            ["RememberMe"] = "false"
        });

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.Equal("/Account/CompanyDeactivated", post.Headers.Location?.ToString());
    }
}
