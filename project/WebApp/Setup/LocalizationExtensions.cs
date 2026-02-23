using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using App.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WebApp.Setup;

public static class LocalizationExtensions
{
    public static IServiceCollection AddAppLocalization(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var supportedCultures = configuration
            .GetSection("SupportedCultures")
            .GetChildren()
            .Select(x => new CultureInfo(x.Value!))
            .ToArray();

        services.Configure<RequestLocalizationOptions>(options =>
        {
            // datetime and currency support
            options.SupportedCultures = supportedCultures;
            // UI translated strings
            options.SupportedUICultures = supportedCultures;
            // if nothing is found, use this
            options.DefaultRequestCulture = new RequestCulture("et-EE", "et-EE");
            options.SetDefaultCulture("et-EE");

            options.RequestCultureProviders = new List<IRequestCultureProvider>
            {
                // Order is important, it's in which order they will be evaluated
                new QueryStringRequestCultureProvider(),
                new CookieRequestCultureProvider(),
                new AcceptLanguageHeaderRequestCultureProvider()
            };
        });

        LangStr.DefaultCulture = configuration.GetValue<string>("LangStrDefaultCulture") ?? "en";

        return services;
    }
}