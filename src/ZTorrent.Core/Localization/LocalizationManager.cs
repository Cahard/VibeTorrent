using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace ZTorrent.Core.Localization;

/// <summary>
/// Process-wide localization source. Keys are natural English sentences, so a
/// missing Russian/Korean translation always falls back to readable English
/// instead of a key or an empty string.
/// </summary>
public sealed class LocalizationManager : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationManager> LazyInstance = new(() => new LocalizationManager());
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _translations;
    private IReadOnlyDictionary<string, string> _current = new Dictionary<string, string>();
    private AppLanguage _language = AppLanguage.English;

    private LocalizationManager()
    {
        _translations = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            [AppLanguage.English.ToCode()] = Load("en"),
            [AppLanguage.Russian.ToCode()] = Load("ru"),
            [AppLanguage.Korean.ToCode()] = Load("ko")
        };
        SetLanguage(AppLanguage.English);
    }

    public static LocalizationManager Instance => LazyInstance.Value;

    public AppLanguage Language => _language;

    public CultureInfo Culture => _language.Culture();

    public int Revision { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? LanguageChanged;

    public string Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        if (_current.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (_translations[AppLanguage.English.ToCode()].TryGetValue(key, out var english) &&
            !string.IsNullOrWhiteSpace(english))
        {
            return english;
        }

        return key;
    }

    public string Format(string key, params object?[] arguments) =>
        string.Format(Culture, Get(key), arguments);

    public void SetLanguage(AppLanguage language)
    {
        _language = language;
        _current = _translations.TryGetValue(language.ToCode(), out var table)
            ? table
            : _translations[AppLanguage.English.ToCode()];

        Revision++;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Revision)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetLanguage(string? code) => SetLanguage(AppLanguages.FromCode(code));

    private static IReadOnlyDictionary<string, string> Load(string language)
    {
        var assembly = typeof(LocalizationManager).Assembly;
        var suffix = $".Localization.{language}.json";
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(resource => resource.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

        if (name is null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = false });
            return values is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(values, StringComparer.Ordinal);
        }
        catch (Exception)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}

public static class L
{
    public static string T(string key) => LocalizationManager.Instance.Get(key);

    public static string F(string key, params object?[] arguments) =>
        LocalizationManager.Instance.Format(key, arguments);

    public static CultureInfo Culture => LocalizationManager.Instance.Culture;
}
