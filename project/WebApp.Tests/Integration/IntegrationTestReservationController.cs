using Microsoft.AspNetCore.Mvc.Testing;

namespace WebApp.Tests.Integration;

[Collection("Database tests")]
public class IntegrationTestReservationController : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public IntegrationTestReservationController(CustomWebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Get_ReservationIndex_AnonymousUser_IsRedirectedToLogin()
    {
        var response = await _client.GetAsync("/Root/Reservation/Index");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_StationDetails_AnonymousUser_IsRedirectedToLogin()
    {
        var response = await _client.GetAsync($"/Root/Station/Details/{Guid.NewGuid()}");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}



