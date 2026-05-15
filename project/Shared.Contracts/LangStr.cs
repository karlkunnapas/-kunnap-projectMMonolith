namespace Shared.Contracts;

public class LangStr : Dictionary<string, string>
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public static string DefaultCulture { get; set; } = "en";

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

        return Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
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

    public static implicit operator string(LangStr? langStr) => langStr?.ToString() ?? "null";
    public static implicit operator LangStr(string value) => new LangStr(value);
}
