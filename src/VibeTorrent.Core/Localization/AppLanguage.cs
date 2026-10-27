using System.Globalization;

namespace VibeTorrent.Core.Localization;

public enum AppLanguage
{
    English,
    Russian,
    Korean
}

public static class AppLanguages
{
    public static string ToCode(this AppLanguage language) => language switch
    {
        AppLanguage.Russian => "ru",
        AppLanguage.Korean => "ko",
        _ => "en"
    };

    public static AppLanguage FromCode(string? code) => code?.Trim().ToLowerInvariant() switch
    {
        "ru" or "ru-ru" => AppLanguage.Russian,
        "ko" or "ko-kr" => AppLanguage.Korean,
        _ => AppLanguage.English
    };

    public static CultureInfo Culture(this AppLanguage language) => language switch
    {
        AppLanguage.Russian => CultureInfo.GetCultureInfo("ru-RU"),
        AppLanguage.Korean => CultureInfo.GetCultureInfo("ko-KR"),
        _ => CultureInfo.GetCultureInfo("en-US")
    };

    public static string NativeName(this AppLanguage language) => language switch
    {
        AppLanguage.Russian => "Русский",
        AppLanguage.Korean => "한국어",
        _ => "English"
    };
}
