using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZTorrent.App.Services;
using ZTorrent.Core.Configuration;
using ZTorrent.Core.Localization;

namespace ZTorrent.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppPaths _paths;
    private readonly IDialogService _dialogs;
    private readonly Action _close;

    public SettingsViewModel(
        AppSettings settings,
        AppPaths paths,
        IDialogService dialogs,
        Action close)
    {
        _paths = paths;
        _dialogs = dialogs;
        _close = close;
        _uiState = settings.Clone();
        LanguageCode = AppLanguages.FromCode(settings.Language).ToCode();
        ThemeCode = settings.Theme;
        AutoCheckUpdates = settings.AutoCheckUpdates;
        AutoInstallUpdates = settings.AutoInstallUpdates;
        UpdateIntervalText = settings.UpdateCheckIntervalHours.ToString(CultureInfo.InvariantCulture);
        AfterDownloadsComplete = settings.AfterDownloadsComplete;

        DownloadDirectory = settings.DownloadDirectory;
        ListenPortText = settings.ListenPort.ToString(CultureInfo.InvariantCulture);
        MaxDownloadText = settings.MaxDownloadKbps.ToString(CultureInfo.InvariantCulture);
        MaxUploadText = settings.MaxUploadKbps.ToString(CultureInfo.InvariantCulture);
        MaxConnectionsText = settings.MaxConnections.ToString(CultureInfo.InvariantCulture);
        UploadSlotsText = settings.MaxUploadSlots.ToString(CultureInfo.InvariantCulture);
        DiskCacheText = settings.DiskCacheMb.ToString(CultureInfo.InvariantCulture);
        EnableDht = settings.EnableDht;
        EnablePex = settings.EnablePex;
        EnableLsd = settings.EnableLsd;
        EnableUpnp = settings.EnableUpnp;
        EnableEncryption = settings.EnableEncryption;
        AutoResume = settings.AutoResumeOnStartup;
        CloseToTray = settings.CloseToTray;
        StartMinimized = settings.StartMinimized;
        StartWithWindows = settings.StartWithWindows;
        ShowNotifications = settings.ShowNotifications;
        _originalPort = settings.ListenPort;
        _initializing = false;
    }

    private readonly int _originalPort;
    private readonly AppSettings _uiState;
    private bool _initializing = true;

    public event EventHandler? Changed;

    [ObservableProperty]
    private string _languageCode = "en";

    [ObservableProperty]
    private string _themeCode = "dark";

    [ObservableProperty]
    private bool _autoCheckUpdates = true;

    [ObservableProperty]
    private bool _autoInstallUpdates = true;

    [ObservableProperty]
    private string _updateIntervalText = "24";

    [ObservableProperty]
    private string _afterDownloadsComplete = "none";

    [ObservableProperty]
    private string _downloadDirectory;

    [ObservableProperty]
    private string _listenPortText;

    [ObservableProperty]
    private string _maxDownloadText;

    [ObservableProperty]
    private string _maxUploadText;

    [ObservableProperty]
    private string _maxConnectionsText;

    [ObservableProperty]
    private string _uploadSlotsText;

    [ObservableProperty]
    private string _diskCacheText;

    [ObservableProperty]
    private bool _enableDht;

    [ObservableProperty]
    private bool _enablePex;

    [ObservableProperty]
    private bool _enableLsd;

    [ObservableProperty]
    private bool _enableUpnp;

    [ObservableProperty]
    private bool _enableEncryption;

    [ObservableProperty]
    private bool _autoResume;

    [ObservableProperty]
    private bool _closeToTray;

    [ObservableProperty]
    private bool _startMinimized;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _showNotifications;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string? _errorMessage;

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!_initializing &&
            e.PropertyName is not (nameof(ErrorMessage) or nameof(IsSaving)))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }


    public string DataDirectory => _paths.RootDirectory;
    public string LogsDirectory => _paths.LogsDirectory;

    public bool IsEnglish => LanguageCode == "en";
    public bool IsRussian => LanguageCode == "ru";
    public bool IsKorean => LanguageCode == "ko";
    public bool IsThemeDark => ThemeCode == "dark";
    public bool IsThemeGreen => ThemeCode == "green";
    public bool IsThemeLight => ThemeCode == "light";
    public bool IsAfterNone => AfterDownloadsComplete == "none";
    public bool IsAfterShutdown => AfterDownloadsComplete == "shutdown";
    public bool IsAfterSleep => AfterDownloadsComplete == "sleep";
    public bool IsAfterHibernate => AfterDownloadsComplete == "hibernate";
    public bool IsAfterRestart => AfterDownloadsComplete == "restart";

    partial void OnThemeCodeChanged(string value)
    {
        OnPropertyChanged(nameof(IsThemeDark));
        OnPropertyChanged(nameof(IsThemeGreen));
        OnPropertyChanged(nameof(IsThemeLight));
    }

    [RelayCommand]
    private void SetTheme(string? code)
    {
        ThemeCode = code?.Trim().ToLowerInvariant() switch
        {
            "green" => "green",
            "light" => "light",
            _ => "dark"
        };
    }

    partial void OnAfterDownloadsCompleteChanged(string value)
    {
        OnPropertyChanged(nameof(IsAfterNone));
        OnPropertyChanged(nameof(IsAfterShutdown));
        OnPropertyChanged(nameof(IsAfterSleep));
        OnPropertyChanged(nameof(IsAfterHibernate));
        OnPropertyChanged(nameof(IsAfterRestart));
    }

    partial void OnLanguageCodeChanged(string value)
    {
        OnPropertyChanged(nameof(IsEnglish));
        OnPropertyChanged(nameof(IsRussian));
        OnPropertyChanged(nameof(IsKorean));
    }

    [RelayCommand]
    private void SetLanguage(string? code)
    {
        LanguageCode = AppLanguages.FromCode(code).ToCode();
    }

    [RelayCommand]
    private void SetAfterDownloadsComplete(string? code)
    {
        var normalized = code?.Trim().ToLowerInvariant();
        AfterDownloadsComplete = normalized is "shutdown" or "sleep" or "hibernate" or "restart"
            ? normalized
            : "none";
    }
    public bool PortRestartHintVisible =>
        int.TryParse(ListenPortText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) &&
        port != _originalPort;

    partial void OnListenPortTextChanged(string value) => OnPropertyChanged(nameof(PortRestartHintVisible));

    [RelayCommand]
    private void BrowseDownloadFolder()
    {
        var folder = _dialogs.PickFolder(DownloadDirectory);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            DownloadDirectory = folder;
        }
    }

    [RelayCommand]
    private void OpenDataFolder() => _dialogs.OpenFolder(_paths.RootDirectory);

    [RelayCommand]
    private void OpenLogsFolder()
    {
        Directory.CreateDirectory(_paths.LogsDirectory);
        _dialogs.OpenFolder(_paths.LogsDirectory);
    }

    [RelayCommand]
    private void Reset()
    {
        var defaults = AppSettings.Default;
        LanguageCode = "en";
        ThemeCode = "dark";
        AutoCheckUpdates = true;
        AutoInstallUpdates = true;
        UpdateIntervalText = "24";
        AfterDownloadsComplete = "none";
        DownloadDirectory = _paths.DownloadsDirectory;
        ListenPortText = defaults.ListenPort.ToString(CultureInfo.InvariantCulture);
        MaxDownloadText = "0";
        MaxUploadText = "0";
        MaxConnectionsText = defaults.MaxConnections.ToString(CultureInfo.InvariantCulture);
        UploadSlotsText = defaults.MaxUploadSlots.ToString(CultureInfo.InvariantCulture);
        DiskCacheText = defaults.DiskCacheMb.ToString(CultureInfo.InvariantCulture);
        EnableDht = true;
        EnablePex = true;
        EnableLsd = true;
        EnableUpnp = true;
        EnableEncryption = true;
        AutoResume = true;
        CloseToTray = true;
        StartMinimized = false;
        StartWithWindows = false;
        ShowNotifications = true;
        ErrorMessage = null;
        OnPropertyChanged(nameof(PortRestartHintVisible));
    }

    [RelayCommand]
    private void Close()
    {
        if (!IsSaving)
        {
            _close();
        }
    }

    public bool TryBuild(out AppSettings settings, out IReadOnlyList<string> errors)
    {
        var list = new List<string>();
        settings = new AppSettings
        {
            DownloadDirectory = DownloadDirectory.Trim(),
            Language = AppLanguages.FromCode(LanguageCode).ToCode(),
            Theme = ThemeCode,
            AutoCheckUpdates = AutoCheckUpdates,
            AutoInstallUpdates = AutoInstallUpdates,
            UpdateCheckIntervalHours = ParseInt(UpdateIntervalText, L.T("Update interval"), list),
            AfterDownloadsComplete = AfterDownloadsComplete.Trim().ToLowerInvariant(),
            ListenPort = ParseInt(ListenPortText, L.T("Port"), list),
            MaxDownloadKbps = ParseInt(MaxDownloadText, L.T("Download limit"), list, allowEmpty: true),
            MaxUploadKbps = ParseInt(MaxUploadText, L.T("Upload limit"), list, allowEmpty: true),
            MaxConnections = ParseInt(MaxConnectionsText, L.T("Connections"), list),
            MaxUploadSlots = ParseInt(UploadSlotsText, L.T("Upload slots"), list),
            DiskCacheMb = ParseInt(DiskCacheText, L.T("Disk cache"), list),
            EnableDht = EnableDht,
            EnablePex = EnablePex,
            EnableLsd = EnableLsd,
            EnableUpnp = EnableUpnp,
            EnableEncryption = EnableEncryption,
            AutoResumeOnStartup = AutoResume,
            CloseToTray = CloseToTray,
            StartMinimized = StartMinimized,
            StartWithWindows = StartWithWindows,
            ShowNotifications = ShowNotifications,
            // These fields are edited by window interactions, not by this screen.
            WindowWidth = _uiState.WindowWidth,
            WindowHeight = _uiState.WindowHeight,
            IsWindowMaximized = _uiState.IsWindowMaximized,
            IsDetailsPaneOpen = _uiState.IsDetailsPaneOpen,
            SortMode = _uiState.SortMode,
            LastFilter = _uiState.LastFilter
        };

        if (!string.IsNullOrWhiteSpace(DownloadDirectory) &&
            !Path.IsPathFullyQualified(DownloadDirectory))
        {
            list.Add(L.T("The download folder must be an absolute path."));
        }

        list.AddRange(settings.Validate(_paths));
        errors = list.Distinct(StringComparer.Ordinal).ToArray();
        ErrorMessage = errors.Count == 0 ? null : string.Join(Environment.NewLine, errors);
        return errors.Count == 0;
    }

    private static int ParseInt(string? text, string field, ICollection<string> errors, bool allowEmpty = false)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            if (allowEmpty)
            {
                return 0;
            }

            errors.Add(L.F("Field \"{0}\" is required.", field));
            return 0;
        }

        if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            errors.Add(L.F("Field \"{0}\" must be an integer.", field));
            return 0;
        }

        if (value < 0)
        {
            errors.Add(L.F("Field \"{0}\" cannot be negative.", field));
            return 0;
        }

        return value;
    }
}
