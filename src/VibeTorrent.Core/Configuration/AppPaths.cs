namespace VibeTorrent.Core.Configuration;

/// <summary>
/// Every path VibeTorrent owns. Keeping paths in one immutable object makes the
/// services testable and guarantees that no component writes outside the app's
/// local data directory.
/// </summary>
public sealed class AppPaths
{
    public required string RootDirectory { get; init; }
    public required string SettingsFile { get; init; }
    public required string SessionFile { get; init; }
    public required string TorrentsDirectory { get; init; }
    public required string FastResumeDirectory { get; init; }
    public required string MetadataDirectory { get; init; }
    public required string CacheDirectory { get; init; }
    public required string LogsDirectory { get; init; }
    public required string DownloadsDirectory { get; init; }
    public required string EngineStateFile { get; init; }

    public static AppPaths CreateDefault()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local))
        {
            local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local");
        }

        var root = Path.Combine(local, "VibeTorrent");
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "VibeTorrent");

        if (string.IsNullOrWhiteSpace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))
        {
            downloads = Path.Combine(root, "Downloads");
        }

        return Create(root, downloads);
    }

    public static AppPaths Create(string root, string downloads)
    {
        var absoluteRoot = Path.GetFullPath(root);
        var cacheDirectory = Path.Combine(absoluteRoot, "cache");
        return new AppPaths
        {
            RootDirectory = absoluteRoot,
            SettingsFile = Path.Combine(absoluteRoot, "settings.json"),
            SessionFile = Path.Combine(absoluteRoot, "session.json"),
            EngineStateFile = Path.Combine(absoluteRoot, "engine.state"),
            TorrentsDirectory = Path.Combine(absoluteRoot, "torrents"),
            FastResumeDirectory = Path.Combine(cacheDirectory, "fastresume"),
            MetadataDirectory = Path.Combine(absoluteRoot, "metadata"),
            CacheDirectory = cacheDirectory,
            LogsDirectory = Path.Combine(absoluteRoot, "logs"),
            DownloadsDirectory = Path.GetFullPath(downloads)
        };
    }

    public void EnsureCreated()
    {
        foreach (var directory in new[]
                 {
                     RootDirectory, TorrentsDirectory, FastResumeDirectory, MetadataDirectory,
                     CacheDirectory, LogsDirectory
                 })
        {
            Directory.CreateDirectory(directory);
        }
    }
}
