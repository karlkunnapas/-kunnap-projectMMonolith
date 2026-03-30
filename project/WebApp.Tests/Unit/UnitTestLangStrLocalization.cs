using App.Domain;

namespace WebApp.Tests.Unit;

public class UnitTestLangStrLocalization
{
    [Fact]
    public void Translate_ReturnsExactCulture_WhenAvailable()
    {
        var text = new LangStr
        {
            ["en"] = "Company not found.",
            ["et"] = "Ettevotet ei leitud."
        };

        var result = text.Translate("et");

        Assert.Equal("Ettevotet ei leitud.", result);
    }

    [Fact]
    public void Translate_ReturnsNeutralCulture_WhenSpecificCultureMissing()
    {
        var text = new LangStr
        {
            ["en"] = "Company not found.",
            ["et"] = "Ettevotet ei leitud."
        };

        var result = text.Translate("et-EE");

        Assert.Equal("Ettevotet ei leitud.", result);
    }

    [Fact]
    public void Translate_ReturnsDefaultCulture_WhenRequestedCultureMissing()
    {
        var originalDefault = LangStr.DefaultCulture;
        try
        {
            LangStr.DefaultCulture = "en";
            var text = new LangStr
            {
                ["en"] = "Company not found.",
                ["et"] = "Ettevotet ei leitud."
            };

            var result = text.Translate("fi-FI");

            Assert.Equal("Company not found.", result);
        }
        finally
        {
            LangStr.DefaultCulture = originalDefault;
        }
    }
}

