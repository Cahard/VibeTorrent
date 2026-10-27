using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VibeTorrent.App.Services;
using VibeTorrent.Core.Configuration;

namespace VibeTorrent.App.ViewModels;

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
    }

    private readonly int _originalPort;
    private readonly AppSettings _uiState;

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

    public string DataDirectory => _paths.RootDirectory;
    public string LogsDirectory => _paths.LogsDirectory;
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
            ListenPort = ParseInt(ListenPortText, "Порт", list),
            MaxDownloadKbps = ParseInt(MaxDownloadText, "Лимит скачивания", list, allowEmpty: true),
            MaxUploadKbps = ParseInt(MaxUploadText, "Лимит отдачи", list, allowEmpty: true),
            MaxConnections = ParseInt(MaxConnectionsText, "Соединения", list),
            MaxUploadSlots = ParseInt(UploadSlotsText, "Слоты отдачи", list),
            DiskCacheMb = ParseInt(DiskCacheText, "Кэш диска", list),
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
            list.Add("Папка загрузок должна иметь абсолютный путь.");
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

            errors.Add($"Поле «{field}» обязательно.");
            return 0;
        }

        if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            errors.Add($"Поле «{field}» должно быть целым числом.");
            return 0;
        }

        if (value < 0)
        {
            errors.Add($"Поле «{field}» не может быть отрицательным.");
            return 0;
        }

        return value;
    }
}
