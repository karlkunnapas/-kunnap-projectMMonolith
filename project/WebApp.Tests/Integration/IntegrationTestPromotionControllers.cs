using System.Net;
using AngleSharp.Html.Dom;
using Modules.Companies.Domain;
using Modules.Companies.Infrastructure;
using Modules.Users.Domain;
using Modules.Users.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using WebApp.Tests.Helpers;

namespace WebApp.Tests.Integration;

[Collection("Database tests")]
public class IntegrationTestPromotionControllers : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public IntegrationTestPromotionControllers(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PromotionWallet_AnonymousUser_IsRedirectedToLogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/Root/Promotion/Index");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task CompanyPromotionIndex_ForeignCompany_ReturnsForbidden()
    {
        var userId = Guid.NewGuid();
        var ownCompany = Guid.NewGuid();
        var foreignCompany = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();

        using (var scope = authFactory.Services.CreateScope())
        {
            var companiesDb = scope.ServiceProvider.GetRequiredService<CompaniesDbContext>();
            var usersDb = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            usersDb.Users.Add(new AppUser { Id = userId, UserName = $"owner-{userId}", Email = $"owner-{userId}@test.local" });
            companiesDb.Companies.AddRange(
                new Company { Id = ownCompany, Name = "Own", ContactEmail = "own@test.local", ContactPhone = "+3721111111", Slug = $"own-{Guid.NewGuid():N}", IsActive = true },
                new Company { Id = foreignCompany, Name = "Foreign", ContactEmail = "foreign@test.local", ContactPhone = "+3722222222", Slug = $"foreign-{Guid.NewGuid():N}", IsActive = true }
            );
            companiesDb.AppUserCompanies.Add(new AppUserCompany
            {
                Id = Guid.NewGuid(),
                AppUserId = userId,
                CompanyId = ownCompany,
                Role = ECompanyRole.Owner,
                IsActive = true,
                JoinedAtUtc = DateTime.UtcNow
            });
            await usersDb.SaveChangesAsync();
            await companiesDb.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(authFactory, userId, "CompanyOwner");
        var response = await client.GetAsync($"/Company/Promotion/Index?companyId={foreignCompany}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PromotionWallet_Redeem_AuthenticatedCustomer_CreatesUserPromotion()
    {
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var code = "SPRING20";
        await using var authFactory = CreateAuthenticatedFactory();

        using (var scope = authFactory.Services.CreateScope())
        {
            var companiesDb = scope.ServiceProvider.GetRequiredService<CompaniesDbContext>();
            var usersDb = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            usersDb.Users.Add(new AppUser { Id = userId, UserName = $"customer-{userId}", Email = $"customer-{userId}@test.local" });
            companiesDb.Companies.Add(new Company
            {
                Id = companyId,
                Name = "Promo Co",
                ContactEmail = "promo@test.local",
                ContactPhone = "+3723333333",
                Slug = $"promo-{Guid.NewGuid():N}",
                IsActive = true
            });
            companiesDb.Promotions.Add(new Promotion
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Code = code,
                DiscountValue = 20m,
                ValidFrom = DateTime.UtcNow.AddDays(-1),
                ValidTo = DateTime.UtcNow.AddDays(10),
                IsActive = true
            });
            await usersDb.SaveChangesAsync();
            await companiesDb.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(authFactory, userId, "Customer");
        var get = await client.GetAsync("/Root/Promotion/Index");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var doc = await HtmlHelpers.GetDocumentAsync(get);
        var form = Assert.IsAssignableFrom<IHtmlFormElement>(doc.QuerySelector("form"));
        var submit = Assert.IsAssignableFrom<IHtmlElement>(Assert.Single(form.QuerySelectorAll("button[type=submit]")));
        var post = await client.SendAsync(form, submit, new Dictionary<string, string>
        {
            ["RedeemCode"] = code
        });

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        using var verifyScope = authFactory.Services.CreateScope();
        var verifyCompaniesDb = verifyScope.ServiceProvider.GetRequiredService<CompaniesDbContext>();
        Assert.True(verifyCompaniesDb.UserPromotions.Any(up => up.UserId == userId && up.Promotion != null && up.Promotion.Code == code));
    }

    private static WebApplicationFactory<Program> CreateAuthenticatedFactory()
    {
        return new CustomWebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                });
            });
    }

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory, Guid userId, params string[] roles)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        return client;
    }
}
