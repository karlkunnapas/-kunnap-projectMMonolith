using Microsoft.AspNetCore.Mvc.Testing;

namespace WebApp.Tests.Integration;

[Collection("Database tests")]
public class IntegrationTestVehicleController : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public IntegrationTestVehicleController(CustomWebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Get_Home_WithVehicleIdQuery_IsSuccessful_ForAnonymousUser()
    {
        var response = await _client.GetAsync($"/?vehicleId={Guid.NewGuid()}");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("name=\"location\"", html);
    }
}
