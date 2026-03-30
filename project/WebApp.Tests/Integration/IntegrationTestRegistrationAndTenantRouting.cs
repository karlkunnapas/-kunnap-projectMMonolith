using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WebApp.Tests.Integration;

[Collection("Database tests")]
public class IntegrationTestRegistrationAndTenantRouting : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public IntegrationTestRegistrationAndTenantRouting(CustomWebApplicationFactory<Program> factory)
    {
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
}


