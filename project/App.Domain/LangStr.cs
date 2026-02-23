namespace App.Domain;

// [Keyless] // maybe use this for testing
public class LangStr : Dictionary<string, string>
{
    // for ef inMemory testing (or use keyless?)
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // look at appsettings.json LangStrDefaultCulture value
    public static string DefaultCulture { get; set; } = "en";

    // s["en"] = "foo";
    // var bar = s["en"];
    public new string this[string key]
    {
        get => base[key];
        set => base[key] = value;
    }

    public LangStr()
    {
    }

    public LangStr(string value) : this(value, Thread.CurrentThread.CurrentUICulture.Name)
    {
    }

    public LangStr(string value, string culture)
    {
        if (culture.Length < 1) throw new ApplicationException("Culture is required!");

        var neutralCulture = culture.Split('-')[0];
        this[neutralCulture] = value;
        
        // check for default culture also. if not set - do so
        if (!ContainsKey(DefaultCulture))
        {
            this[DefaultCulture] = value;
        }
    }

    public string? Translate(string? culture = null)
    {
        if (Count == 0) return null;
        culture = culture?.Trim() ?? Thread.CurrentThread.CurrentUICulture.Name;

        if (ContainsKey(culture))
        {
            return this[culture];
        }

        var neutralCulture = culture.Split('-')[0];
        if (ContainsKey(neutralCulture))
        {
            return this[neutralCulture];
        }

        if (ContainsKey(DefaultCulture))
        {
            return this[DefaultCulture];
        }

        return null;
    }

    public void SetTranslation(string value, string? culture = null)
    {
        culture = culture?.Trim() ?? Thread.CurrentThread.CurrentUICulture.Name;
        var neutralCulture = culture.Split('-')[0];
        this[neutralCulture] = value;
    }

    public override string ToString()
    {
        return Translate() ?? "????";
    }

    // string xxx = new LangStr("foo","et-EE"); xxx == "foo";
    public static implicit operator string(LangStr? langStr) => langStr?.ToString() ?? "null";

    // LangStr xxx = "foobar";
    public static implicit operator LangStr(string value) => new LangStr(value);
}
