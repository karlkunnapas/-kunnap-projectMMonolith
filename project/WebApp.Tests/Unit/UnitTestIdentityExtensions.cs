using System.Security.Claims;
using WebApp.Helpers;

namespace WebApp.Tests.Unit;

public class UnitTestIdentityExtensions
{
    [Fact]
    public void UserId_WithDuplicateNameIdentifierClaims_ReturnsFirstMatch()
    {
        var userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "test"));

        var resolved = principal.UserId();

        Assert.Equal(userId, resolved);
    }

    [Fact]
    public void UserId_UsesSubFallback_WhenNameIdentifierMissing()
    {
        var userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", userId.ToString())
        }, "test"));

        var resolved = principal.UserId();

        Assert.Equal(userId, resolved);
    }
}
