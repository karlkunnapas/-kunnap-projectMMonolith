using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using App.DTO.v1.Identity;
using App.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace WebApp.Tests.Integration.Api;

[Collection("Database tests")]
public class IntegrationTestIdentity : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;


    public IntegrationTestIdentity(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Theory]
    [InlineData("First.Last.1", "first@last.com")]
    public async Task Registration_Flow(string dataPassword, string dataEmail)
    {
        // Arrange
        var data = new Register()
        {
            Password = dataPassword,
            Email = dataEmail,
        };

        // Act
        var response = await _client.PostAsync(
            "/api/v1/account/register",
            new StringContent(JsonSerializer.Serialize(data, JsonHelpers.JsonSerializerOptionsCamelCase), Encoding.UTF8,
                "application/json")
        );

        // Assert
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        var jwtResponse =
            JsonSerializer.Deserialize<JWTResponse>(responseString, JsonHelpers.JsonSerializerOptionsCamelCase);
        Assert.NotNull(jwtResponse);
    }

    [Theory]
    [InlineData("First.Last.1", "login@last.com")]
    public async Task Login_Flow(string dataPassword, string dataEmail)
    {
        // Arrange
        var data = new Register()
        {
            Password = dataPassword,
            Email = dataEmail,
        };


        var response = await _client.PostAsync(
            "/api/v1/account/register",
            new StringContent(
                System.Text.Json.JsonSerializer.Serialize(data, JsonHelpers.JsonSerializerOptionsCamelCase),
                Encoding.UTF8, "application/json")
        );
        response.EnsureSuccessStatusCode();


        var loginData = new Login()
        {
            Email = dataEmail,
            Password = dataPassword,
        };


        // Act

        var loginResponse = await _client.PostAsync(
            "/api/v1/account/login",
            new StringContent(JsonSerializer.Serialize(loginData, JsonHelpers.JsonSerializerOptionsCamelCase),
                Encoding.UTF8, "application/json")
        );


        // Assert
        response.EnsureSuccessStatusCode();

        var responseString = await loginResponse.Content.ReadAsStringAsync();
        var jwtResponse =
            JsonSerializer.Deserialize<JWTResponse>(responseString, JsonHelpers.JsonSerializerOptionsCamelCase);
        Assert.NotNull(jwtResponse);
    }
}