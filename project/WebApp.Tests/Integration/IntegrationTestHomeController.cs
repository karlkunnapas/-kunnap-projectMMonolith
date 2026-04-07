using Microsoft.AspNetCore.Mvc.Testing;

namespace WebApp.Tests.Integration;

[Collection("Database tests")]
public class IntegrationTestHomeController : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;


    public IntegrationTestHomeController(CustomWebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }


    [Fact]
    public async Task Get_Index_IsSuccessful()
    {
        // Arrange
            
        // Act
        var response = await _client.GetAsync("/");
        
        // Assert
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Fast Charging", html);
    }

    [Fact]
    public async Task Get_Index_StatusFilter_PersistsSelectedStatusInForm()
    {
        // Arrange

        // Act
        var response = await _client.GetAsync("/?status=Available");

        // Assert
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("name=\"status\" value=\"Available\"", html);
    }

    [Fact]
    public async Task Get_Index_ConnectorFilter_PersistsSelectedConnectorInForm()
    {
        // Arrange

        // Act
        var response = await _client.GetAsync("/?connector=CHAdeMO");

        // Assert
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("name=\"connector\" value=\"CHAdeMO\"", html);
    }

    [Fact]
    public async Task Get_Index_LocationFilter_PersistsLocationInputValue()
    {
        // Arrange

        // Act
        var response = await _client.GetAsync("/?location=3.8");

        // Assert
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("name=\"location\" value=\"3.8\"", html);
    }
}