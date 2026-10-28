using System.Text.Json.Serialization;
using ZTorrent.Core.Localization;

namespace ZTorrent.Core.Configuration;

/// <summary>
/// User-facing settings. Validation is explicit so the settings screen can show
/// human-readable errors instead of silently clamping values.
/// </summary>
public sealed class AppSettings
{
    public const int MinimumAlternatePort = 1024;
    public const int MaximumAlternatePort = 65535;

    public string DownloadDirectory { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public string AfterDownloadsComplete { get; set; } = "none";
    public string Theme { get; set; } = "dark";
    public bool AutoCheckUpdates { get; set; } = true;
    public bool AutoInstallUpdates { get; set; } = true;
    public int UpdateCheckIntervalHours { get; set; } = 24;
    public DateTime? LastUpdateCheckUtc { get; set; }
    public int ListenPort { get; set; } = 55123;
    public int MaxDownloadKbps { get; set; }
    public int MaxUploadKbps { get; set; }
    public int MaxConnections { get; set; } = 200;
    public int MaxUploadSlots { get; set; } = 4;
    public int DiskCacheMb { get; set; } = 64;
    public bool EnableDht { get; set; } = true;
    public bool EnablePex { get; set; } = true;
    public bool EnableLsd { get; set; } = true;
    public bool EnableUpnp { get; set; } = true;
    public bool EnableEncryption { get; set; } = true;
    public bool AutoResumeOnStartup { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool StartMinimized { get; set; }
    public bool StartWithWindows { get; set; }
    public bool ShowNotifications { get; set; } = true;
    public bool IsDetailsPaneOpen { get; set; } = true;
    public bool CompactList { get; set; }
    public double WindowWidth { get; set; } = 1440;
    public double WindowHeight { get; set; } = 900;
    public bool IsWindowMaximized { get; set; }
    public string SortMode { get; set; } = "added";
    public string LastFilter { get; set; } = "all";

    [JsonIgnore]
    public static AppSettings Default => new();

    public AppSettings Clone() => new()
    {
        DownloadDirectory = DownloadDirectory,
        Language = Language,
        AfterDownloadsComplete = AfterDownloadsComplete,
        Theme = Theme,
        AutoCheckUpdates = AutoCheckUpdates,
        AutoInstallUpdates = AutoInstallUpdates,
        UpdateCheckIntervalHours = UpdateCheckIntervalHours,
        LastUpdateCheckUtc = LastUpdateCheckUtc,
        ListenPort = ListenPort,
        MaxDownloadKbps = MaxDownloadKbps,
        MaxUploadKbps = MaxUploadKbps,
        MaxConnections = MaxConnections,
        MaxUploadSlots = MaxUploadSlots,
        DiskCacheMb = DiskCacheMb,
        EnableDht = EnableDht,
        EnablePex = EnablePex,
        EnableLsd = EnableLsd,
        EnableUpnp = EnableUpnp,
        EnableEncryption = EnableEncryption,
        AutoResumeOnStartup = AutoResumeOnStartup,
        CloseToTray = CloseToTray,
        StartMinimized = StartMinimized,
        StartWithWindows = StartWithWindows,
        ShowNotifications = ShowNotifications,
        IsDetailsPaneOpen = IsDetailsPaneOpen,
        CompactList = CompactList,
        WindowWidth = WindowWidth,
        WindowHeight = WindowHeight,
        IsWindowMaximized = IsWindowMaximized,
        SortMode = SortMode,
        LastFilter = LastFilter
    };

    public IReadOnlyList<string> Validate(AppPaths paths)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(DownloadDirectory))
        {
            errors.Add(L.T("Choose a download folder."));
        }
        else if (!Path.IsPathFullyQualified(DownloadDirectory))
        {
            errors.Add(L.T("The download folder must be an absolute path."));
        }
        else if (DownloadDirectory.StartsWith(@"\\", StringComparison.Ordinal))
        {
            errors.Add(L.T("Network UNC folders are not supported. Choose a local drive."));
        }
        else
        {
            try
            {
                _ = Path.GetFullPath(DownloadDirectory);
            }
            catch (Exception)
            {
                errors.Add(L.T("The download folder path contains invalid characters."));
            }
        }

        if (ListenPort is < MinimumAlternatePort or > MaximumAlternatePort)
        {
            errors.Add(L.F("The port must be between {0} and {1}.", MinimumAlternatePort, MaximumAlternatePort));
        }

        if (MaxDownloadKbps is < 0 or > 10_000_000)
        {
            errors.Add(L.T("The download limit must be between 0 and 10,000,000 KB/s."));
        }

        if (MaxUploadKbps is < 0 or > 10_000_000)
        {
            errors.Add(L.T("The upload limit must be between 0 and 10,000,000 KB/s."));
        }

        if (MaxConnections is < 20 or > 2000)
        {
            errors.Add(L.T("Maximum connections must be between 20 and 2,000."));
        }

        if (MaxUploadSlots is < 1 or > 100)
        {
            errors.Add(L.T("Upload slots must be between 1 and 100."));
        }

        if (DiskCacheMb is < 8 or > 1024)
        {
            errors.Add(L.T("The disk cache must be between 8 and 1,024 MB."));
        }

        if (UpdateCheckIntervalHours is < 1 or > 720)
        {
            errors.Add(L.T("The update check interval must be between 1 and 720 hours."));
        }

        if (paths.RootDirectory.Length > 200)
        {
            errors.Add(L.T("The application data path is too long."));
        }

        return errors;
    }

    /// <summary>
    /// Repairs values loaded from an older or hand-edited file. Returns true when
    /// anything changed. The UI still validates the user's current input.
    /// </summary>
    public bool Normalize()
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(DownloadDirectory) ||
            !Path.IsPathFullyQualified(DownloadDirectory))
        {
            DownloadDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads",
                "ZTorrent");
            changed = true;
        }

        var port = ClampValue(ListenPort, MinimumAlternatePort, MaximumAlternatePort, 55123);
        if (port.Changed)
        {
            ListenPort = port.Value;
            changed = true;
        }

        var download = ClampValue(MaxDownloadKbps, 0, 10_000_000, 0);
        if (download.Changed)
        {
            MaxDownloadKbps = download.Value;
            changed = true;
        }

        var upload = ClampValue(MaxUploadKbps, 0, 10_000_000, 0);
        if (upload.Changed)
        {
            MaxUploadKbps = upload.Value;
            changed = true;
        }

        var connections = ClampValue(MaxConnections, 20, 2000, 200);
        if (connections.Changed)
        {
            MaxConnections = connections.Value;
            changed = true;
        }

        var slots = ClampValue(MaxUploadSlots, 1, 100, 4);
        if (slots.Changed)
        {
            MaxUploadSlots = slots.Value;
            changed = true;
        }

        var cache = ClampValue(DiskCacheMb, 8, 1024, 64);
        if (cache.Changed)
        {
            DiskCacheMb = cache.Value;
            changed = true;
        }

        if (WindowWidth is < 1100 or > 10000)
        {
            WindowWidth = 1440;
            changed = true;
        }

        if (WindowHeight is < 700 or > 10000)
        {
            WindowHeight = 900;
            changed = true;
        }

        var normalizedLanguage = Language?.Trim().ToLowerInvariant();
        if (normalizedLanguage is not ("en" or "ru" or "ko"))
        {
            Language = "en";
            changed = true;
        }
        else if (Language != normalizedLanguage)
        {
            Language = normalizedLanguage;
            changed = true;
        }

        var normalizedAction = AfterDownloadsComplete?.Trim().ToLowerInvariant();
        if (normalizedAction is not ("none" or "shutdown" or "sleep" or "hibernate" or "restart"))
        {
            AfterDownloadsComplete = "none";
            changed = true;
        }
        else if (AfterDownloadsComplete != normalizedAction)
        {
            AfterDownloadsComplete = normalizedAction;
            changed = true;
        }

        var normalizedTheme = Theme?.Trim().ToLowerInvariant();
        if (normalizedTheme is not ("dark" or "green" or "light"))
        {
            Theme = "dark";
            changed = true;
        }
        else if (Theme != normalizedTheme)
        {
            Theme = normalizedTheme;
            changed = true;
        }

        var updateHours = Math.Clamp(UpdateCheckIntervalHours, 1, 720);
        if (updateHours != UpdateCheckIntervalHours)
        {
            UpdateCheckIntervalHours = updateHours;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(SortMode))
        {
            SortMode = "added";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(LastFilter))
        {
            LastFilter = "all";
            changed = true;
        }

        return changed;
    }

    private static (int Value, bool Changed) ClampValue(int value, int min, int max, int fallback)
    {
        if (value >= min && value <= max)
        {
            return (value, false);
        }

        var clamped = value < min ? min : max;
        if (clamped == 0)
        {
            clamped = fallback;
        }

        return (clamped, true);
    }
}
