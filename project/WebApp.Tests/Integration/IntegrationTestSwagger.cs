using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WebApp.Tests.Integration;

public class IntegrationTestSwagger : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public IntegrationTestSwagger(CustomWebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Swagger_DoesNotExposeFrameworkProblemDetailsSchema()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var swaggerJson = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Microsoft.AspNetCore.Mvc.ProblemDetails", swaggerJson);
    }
}
