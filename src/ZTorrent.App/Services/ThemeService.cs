using System.Windows;

namespace ZTorrent.App.Services;

/// <summary>
/// Swaps the two theme dictionaries. Every themed brush in the XAML uses a
/// DynamicResource, so applying a theme updates the live window without a
/// restart.
/// </summary>
public static class ThemeService
{
    public static string CurrentCode { get; private set; } = "dark";

    public static bool IsLight => CurrentCode == "light";

    public static event Action? ThemeChanged;

    public static void Apply(string? code)
    {
        var normalized = code?.Trim().ToLowerInvariant() switch
        {
            "green" => "green",
            "light" => "light",
            _ => "dark"
        };

        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        if (CurrentCode == normalized)
        {
            return;
        }

        var dictionaries = app.Resources.MergedDictionaries;
        var fileName = normalized switch
        {
            "green" => "Green",
            "light" => "Light",
            _ => "Dark"
        };

        Replace(dictionaries, "Themes/Tokens.", $"Themes/Tokens.{fileName}.xaml");
        Replace(dictionaries, "Themes/ThemeColors.", $"Themes/ThemeColors.{fileName}.xaml");

        CurrentCode = normalized;
        ThemeChanged?.Invoke();
    }

    private static void Replace(
        System.Collections.ObjectModel.Collection<ResourceDictionary> dictionaries,
        string marker,
        string source)
    {
        var index = -1;
        for (var i = 0; i < dictionaries.Count; i++)
        {
            var original = dictionaries[i].Source?.OriginalString;
            if (original?.Contains(marker, StringComparison.OrdinalIgnoreCase) == true)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            return;
        }

        dictionaries[index] = new ResourceDictionary
        {
            Source = new Uri(source, UriKind.Relative)
        };
    }
}
