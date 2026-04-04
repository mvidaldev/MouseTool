using System.Globalization;
using System.IO;
using System.Text.Json;

namespace MouseTool;

internal static class AppLocalizer
{
    private static readonly Dictionary<string, Dictionary<string, string>> Resources = new(StringComparer.OrdinalIgnoreCase);
    private static readonly (string Code, string Key)[] SupportedLanguages =
    [
        (string.Empty, "LanguageSystem"),
        ("en", "LanguageEnglish"),
        ("pt-BR", "LanguagePortugueseBrazil"),
        ("fr", "LanguageFrench"),
        ("es", "LanguageSpanish"),
        ("de", "LanguageGerman"),
        ("it", "LanguageItalian"),
        ("ru", "LanguageRussian")
    ];

    public static void Initialize(string directory)
    {
        Resources.Clear();

        foreach (var path in Directory.GetFiles(directory, "lang.*.json"))
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            var languageCode = fileName["lang.".Length..];
            LoadLanguageFile(directory, languageCode);
        }
    }

    private static void LoadLanguageFile(string directory, string languageCode)
    {
        var path = Path.Combine(directory, $"lang.{languageCode}.json");
        if (!File.Exists(path))
        {
            return;
        }

        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        Resources[languageCode] = new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);
    }

    public static string ResolveLanguageCode(string? preferredCode)
    {
        if (!string.IsNullOrWhiteSpace(preferredCode) && Resources.ContainsKey(preferredCode))
        {
            return preferredCode;
        }

        var system = CultureInfo.InstalledUICulture.Name;
        if (Resources.ContainsKey(system))
        {
            return system;
        }

        var neutral = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName;
        if (!string.IsNullOrWhiteSpace(neutral) && Resources.ContainsKey(neutral))
        {
            return neutral;
        }

        if (system.StartsWith("pt", StringComparison.OrdinalIgnoreCase) && Resources.ContainsKey("pt-BR"))
        {
            return "pt-BR";
        }

        return Resources.ContainsKey("en") ? "en" : Resources.Keys.FirstOrDefault() ?? "en";
    }

    public static CultureInfo ResolveCulture(string? preferredCode) => new(ResolveLanguageCode(preferredCode));

    public static string Get(string? preferredCode, string key)
    {
        var code = ResolveLanguageCode(preferredCode);
        if (Resources.TryGetValue(code, out var resource) && resource.TryGetValue(key, out var value))
        {
            return value;
        }

        if (Resources.TryGetValue("en", out var fallback) && fallback.TryGetValue(key, out var english))
        {
            return english;
        }

        return key;
    }

    public static IReadOnlyList<LanguageOption> GetLanguageOptions(string? preferredCode)
    {
        return SupportedLanguages
            .Where(language => string.IsNullOrWhiteSpace(language.Code) || Resources.ContainsKey(language.Code))
            .Select(language => new LanguageOption
            {
                Code = language.Code,
                DisplayName = Get(preferredCode, language.Key)
            })
            .ToList();
    }
}
