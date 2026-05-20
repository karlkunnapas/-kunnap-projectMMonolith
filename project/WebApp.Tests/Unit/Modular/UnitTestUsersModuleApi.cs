using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Users;

namespace WebApp.Tests.Unit;

public class UnitTestUsersModuleApi : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public UnitTestUsersModuleApi(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthenticateByEmail_ReturnsUserAndClaimsForSeedCustomer()
    {
        using var scope = _factory.Services.CreateScope();
        var api = scope.ServiceProvider.GetRequiredService<IUsersModuleApi>();

        var auth = await api.AuthenticateByEmailAsync("karl@karl.com", "Karl.123");

        Assert.True(auth.Success);
        Assert.NotEqual(Guid.Empty, auth.UserId);

        var claims = await api.GetJwtClaimsAsync(auth.UserId);
        Assert.Contains(claims, c => c.Type.Contains("email") && c.Value == "karl@karl.com");
    }

    [Fact]
    public async Task RegisterCustomer_CreatesUserDiscoverableByEmail()
    {
        using var scope = _factory.Services.CreateScope();
        var api = scope.ServiceProvider.GetRequiredService<IUsersModuleApi>();

        var email = $"module-user-{Guid.NewGuid():N}@test.local";
        var result = await api.RegisterCustomerAsync(new RegisterCustomerContract
        {
            Email = email,
            Password = "Module.123",
            FirstName = "Module",
            LastName = "User"
        });

        Assert.True(result.Success);
        var userId = await api.GetUserIdByEmailAsync(email);
        Assert.NotNull(userId);
    }
}
