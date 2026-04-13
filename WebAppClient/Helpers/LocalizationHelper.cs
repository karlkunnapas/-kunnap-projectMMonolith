using System.Globalization;

namespace WebAppClient.Helpers;

public static class LocalizationHelper
{
    public static string GetLocalizedName(string defaultName, Dictionary<string, string>? translations)
    {
        if (translations == null || translations.Count == 0)
        {
            return defaultName;
        }

        var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (translations.TryGetValue(culture, out var localized) && !string.IsNullOrWhiteSpace(localized))
        {
            return localized;
        }

        return defaultName;
    }
}
