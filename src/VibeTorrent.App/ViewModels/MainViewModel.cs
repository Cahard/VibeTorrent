using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using VibeTorrent.App.Demo;
using VibeTorrent.App.Services;
using VibeTorrent.Core.Configuration;
using VibeTorrent.Core.Localization;
using VibeTorrent.Core.Models;
using VibeTorrent.Core.Services;
using VibeTorrent.Core.Utilities;

namespace VibeTorrent.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private const int HistoryLength = 48;

    private readonly ITorrentEngineService _engine;
    private readonly ISettingsService _settingsService;
    private readonly ITorrentPreviewService _previewService;
    private readonly AppPaths _paths;
    private readonly IDialogService _dialogs;
    private readonly IStartupService _startupService;
    private readonly IUpdateService _updates;
    private readonly IUiDispatcher _ui;
    private readonly ILogger<MainViewModel> _logger;

    private readonly ObservableCollection<TorrentItemViewModel> _torrents = [];
    private readonly Dictionary<string, TorrentItemViewModel> _index = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _previousComplete = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TorrentStatus> _previousStatus = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<double> _downloadSamplesQueue = new();
    private readonly Queue<double> _uploadSamplesQueue = new();
    private readonly DispatcherTimer _storageTimer;
    private readonly DispatcherTimer _settingsSaveTimer;
    private readonly DispatcherTimer _updateTimer;
    private bool _settingsSaveInFlight;
    private bool _updateCheckInFlight;
    private bool _updateDownloadInFlight;
    private UpdateInfo? _pendingUpdate;

    private AppSettings _settings = AppSettings.Default;
    private CancellationTokenSource? _detailsCts;
    private bool _suppressNotifications;
    private bool _completionActionArmed;
    private bool _completionActionRaised;
    private bool _hasSeenSnapshot;
    private bool _disposed;

    public event Action<string>? AllDownloadsCompleted;

    public int ActiveDownloadCount => _torrents.Count(t =>
        t.Status is TorrentStatus.Metadata or TorrentStatus.Checking
            or TorrentStatus.Downloading or TorrentStatus.Queued);

    public MainViewModel(
        ITorrentEngineService engine,
        ISettingsService settingsService,
        ITorrentPreviewService previewService,
        AppPaths paths,
        IDialogService dialogs,
        IStartupService startupService,
        IUpdateService updates,
        IUiDispatcher ui,
        ILogger<MainViewModel> logger)
    {
        _engine = engine;
        _settingsService = settingsService;
        _previewService = previewService;
        _paths = paths;
        _dialogs = dialogs;
        _startupService = startupService;
        _updates = updates;
        _ui = ui;
        _logger = logger;

        TorrentsView = CollectionViewSource.GetDefaultView(_torrents);
        TorrentsView.Filter = FilterTorrent;
        ApplySort(refresh: false);

        for (var i = 0; i < HistoryLength; i++)
        {
            _downloadSamplesQueue.Enqueue(0);
            _uploadSamplesQueue.Enqueue(0);
        }

        DownloadHistory = _downloadSamplesQueue.ToArray();
        UploadHistory = _uploadSamplesQueue.ToArray();
        _storageTimer = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Background,
            (_, _) => RefreshStorage(), Dispatcher.CurrentDispatcher);
        DispatcherTimer? settingsSaveTimer = null;
        settingsSaveTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(450), DispatcherPriority.Background,
            (_, _) =>
            {
                settingsSaveTimer!.Stop();
                _ = SaveSettingsPageAsync(SettingsPage);
            }, Dispatcher.CurrentDispatcher);
        _settingsSaveTimer = settingsSaveTimer;
        DispatcherTimer? updateTimer = null;
        updateTimer = new DispatcherTimer(TimeSpan.FromHours(24), DispatcherPriority.Background,
            (_, _) => _ = CheckForUpdatesAsync(automatic: true), Dispatcher.CurrentDispatcher);
        _updateTimer = updateTimer;
        UpdateButtonText = L.T("Check for updates");
        LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
    }

    public event Action<string, string, bool>? NotificationRequested;

    public ObservableCollection<TorrentItemViewModel> Torrents => _torrents;
    public ICollectionView TorrentsView { get; }
    public ObservableCollection<ToastViewModel> Toasts { get; } = [];

    public bool IsDemoMode { get; private set; }

    [ObservableProperty]
    private AppSettings _currentSettings = AppSettings.Default;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = "all";

    [ObservableProperty]
    private string _sortMode = "added";

    [ObservableProperty]
    private TorrentItemViewModel? _selectedTorrent;

    [ObservableProperty]
    private string _updateButtonText = "Check for updates";

    [ObservableProperty]
    private string _updateStatusText = string.Empty;


    [ObservableProperty]
    private TorrentDetailsViewModel? _details;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private bool _isInitialized;

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    private SettingsViewModel? _settingsPage;

    [ObservableProperty]
    private bool _isAddDialogOpen;

    [ObservableProperty]
    private AddTorrentViewModel? _addDialog;

    [ObservableProperty]
    private bool _isConfirmRemoveOpen;

    [ObservableProperty]
    private ConfirmRemoveViewModel? _confirmRemove;

    [ObservableProperty]
    private bool _isDragOver;

    [ObservableProperty]
    private bool _isDetailsPaneOpen = true;

    [ObservableProperty]
    private bool _isCompactView;

    [ObservableProperty]
    private string _selectedDownloadLimitText = "0";

    [ObservableProperty]
    private string _selectedUploadLimitText = "0";

    [ObservableProperty]
    private SessionStatsSnapshot _stats = new();

    [ObservableProperty]
    private double[] _downloadHistory = [];

    [ObservableProperty]
    private double[] _uploadHistory = [];

    [ObservableProperty]
    private string _storageFreeText = "—";

    [ObservableProperty]
    private string _storageTotalText = "—";

    [ObservableProperty]
    private double _storageUsedPercent;

    [ObservableProperty]
    private string _storagePathText = "—";

    [ObservableProperty]
    private string _uptimeText = "—";

    public int AllCount => _torrents.Count;
    public int ActiveCount => _torrents.Count(t => t.IsActive);
    public int CompletedCount => _torrents.Count(t => t.IsComplete);
    public int PausedCount => _torrents.Count(t => t.IsPaused);
    public int ErrorCount => _torrents.Count(t => t.IsError);
    public int FilteredCount => _torrents.Count(FilterTorrent);

    public string DownloadSpeedText => DisplayFormat.Speed(Stats.DownloadSpeed);
    public string UploadSpeedText => DisplayFormat.Speed(Stats.UploadSpeed);
    public string DownloadedTotalText => DisplayFormat.Bytes(Stats.DownloadedBytes);
    public string UploadedTotalText => DisplayFormat.Bytes(Stats.UploadedBytes);
    public string DhtStatusText => !Stats.DhtEnabled
        ? L.T("DHT is off")
        : L.F("{0} · {1:N0} nodes", Stats.DhtState, Stats.DhtNodes);
    public string ListenPortText => Stats.ListenPort > 0
        ? L.F("Port {0}", Stats.ListenPort)
        : L.T("Port: auto");
    public string EngineStatusText => Stats.IsEngineRunning
        ? L.T("Network active")
        : L.T("Network stopped");
    public string ActiveCaption => L.F("{0} active", ActiveCount);
    public string HeaderTitle => IsSettingsOpen
        ? L.T("Settings")
        : SelectedFilter switch
        {
            "active" => L.T("Active"),
            "completed" => L.T("Completed"),
            "paused" => L.T("Paused"),
            "error" => L.T("Error"),
            _ => L.T("All downloads")
        };
    public string HeaderEyebrow => IsSettingsOpen ? L.T("CONFIGURATION") : L.T("LIBRARY");
    public string HeaderSubtitle => IsSettingsOpen
        ? L.T("Network, speed and storage — all under control.")
        : _torrents.Count == 0
            ? L.T("Add your first torrent — a file or a magnet link.")
            : L.F("{0} downloads · {1} active", AllCount, ActiveCount);
    public string FilterSummaryText => SearchText.Length > 0
        ? L.F("Found: {0}", FilteredCount)
        : L.F("{0} torrents", FilteredCount);
    public string UptimeLabel => L.F("Uptime {0}", UptimeText);
    public string DownloadedTotalLabel => L.F("Total {0}", DownloadedTotalText);
    public string UploadedTotalLabel => L.F("Total {0}", UploadedTotalText);

    public bool IsFilterAll => SelectedFilter == "all";
    public bool IsFilterActive => SelectedFilter == "active";
    public bool IsFilterCompleted => SelectedFilter == "completed";
    public bool IsFilterPaused => SelectedFilter == "paused";
    public bool IsFilterError => SelectedFilter == "error";
    public bool HasAnyTorrents => _torrents.Count > 0;
    public bool IsLibraryEmpty => !IsLoading && _torrents.Count == 0;
    public bool IsFilterEmpty => !IsLoading && _torrents.Count > 0 && FilteredCount == 0;
    public bool ShowTorrentList => !IsLoading && _torrents.Count > 0 && FilteredCount > 0;
    public bool HasSearch => !string.IsNullOrWhiteSpace(SearchText);
    public bool IsSearchEmpty => HasSearch && FilteredCount == 0;
    public bool ShowDetailsPane => IsDetailsPaneOpen && SelectedTorrent is not null && !IsSettingsOpen;
    public bool ShowCardList => ShowTorrentList && !IsCompactView;
    public bool ShowCompactList => ShowTorrentList && IsCompactView;
    public string EmptyStateTitle => IsSearchEmpty ? L.T("No results") : L.T("Nothing here yet");
    public string EmptyStateDescription => IsSearchEmpty
        ? L.T("Try a different query or reset the filters.")
        : L.T("Drop a .torrent file or add a magnet link. Downloads will appear here.");
    public string SortLabel => SortMode switch
    {
        "name" => L.T("Name"),
        "progress" => L.T("Progress"),
        "size" => L.T("Size"),
        "speed" => L.T("Speed"),
        "ratio" => L.T("Ratio"),
        _ => L.T("Newest first")
    };

    public double DownloadHistoryMaximum => Math.Max(1, DownloadHistory.DefaultIfEmpty(0).Max());
    public double UploadHistoryMaximum => Math.Max(1, UploadHistory.DefaultIfEmpty(0).Max());

    public async Task InitializeAsync(
        AppSettings settings,
        IReadOnlyList<string> startupArguments,
        bool demoMode,
        CancellationToken cancellationToken = default)
    {
        _settings = settings.Clone();
        CurrentSettings = _settings;
        IsDemoMode = demoMode;
        IsDetailsPaneOpen = settings.IsDetailsPaneOpen;
        IsCompactView = settings.CompactList;
        SortMode = string.IsNullOrWhiteSpace(settings.SortMode) ? "added" : settings.SortMode;
        SelectedFilter = string.IsNullOrWhiteSpace(settings.LastFilter) ? "all" : settings.LastFilter;
        CurrentSettings = _settings;

        _storageTimer.Start();
        RefreshStorage();

        if (demoMode)
        {
            LoadDemoLibrary();
            IsLoading = false;
            IsInitialized = true;
            return;
        }

        _engine.SnapshotUpdated += OnEngineSnapshot;
        var engineReady = true;
        try
        {
            await _engine.InitializeAsync(cancellationToken);
            ApplySnapshot(_engine.CurrentSnapshot);
        }
        catch (Exception ex)
        {
            engineReady = false;
            _logger.LogError(ex, "Engine initialization failed.");
            ShowToast(L.T("Could not start the network"), ex.Message, ToastKind.Error);
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsLibraryEmpty));
            OnPropertyChanged(nameof(IsFilterEmpty));
        }

        if (engineReady)
        {
            await ProcessStartupArgumentsAsync(startupArguments, cancellationToken);
        }

        IsInitialized = engineReady;
        RestartUpdateTimer();
        _ = RunInitialUpdateCheckAsync();
        if (!string.IsNullOrWhiteSpace(Stats.Warning))
        {
            ShowToast(L.T("Network"), Stats.Warning, ToastKind.Warning);
        }

        if (SelectedTorrent is null && _torrents.Count > 0 && IsDetailsPaneOpen)
        {
            SelectedTorrent = _torrents[0];
        }
    }

    public async Task ProcessExternalArgumentsAsync(string[] args)
    {
        if (IsDemoMode || args.Length == 0)
        {
            return;
        }

        await ProcessStartupArgumentsAsync(args, CancellationToken.None);
    }

    public async Task HandleDroppedDataAsync(IReadOnlyList<string> files, string? text)
    {
        var torrents = files.Where(path => path.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
            .Where(File.Exists)
            .ToArray();

        if (torrents.Length == 1)
        {
            var path = torrents[0];
            await OpenAddDialogForFileAsync(path);
            return;
        }

        if (torrents.Length > 1)
        {
            foreach (var path in torrents)
            {
                await AddTorrentDirectlyAsync(path, null);
            }

            return;
        }

        var magnet = NormalizeMagnet(text) ?? files.Select(NormalizeMagnet).FirstOrDefault(m => m is not null);
        if (magnet is not null)
        {
            OpenAddDialog(AddMode.Magnet);
            if (AddDialog is not null)
            {
                AddDialog.MagnetText = magnet;
            }
        }
    }

    public async Task PasteMagnetFromClipboardAsync()
    {
        try
        {
            if (!Clipboard.ContainsText())
            {
                return;
            }

            var text = Clipboard.GetText().Trim();
            if (text.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
            {
                OpenAddDialog(AddMode.Magnet);
                if (AddDialog is not null)
                {
                    AddDialog.MagnetText = text;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Clipboard read failed.");
        }
    }

    [RelayCommand]
    private void OpenAddTorrent() => OpenAddDialog(AddMode.File);

    [RelayCommand]
    private void OpenMagnet() => OpenAddDialog(AddMode.Magnet);

    [RelayCommand]
    private void CloseAddDialog()
    {
        if (AddDialog?.IsBusy == true)
        {
            return;
        }

        AddDialog?.Dispose();
        AddDialog = null;
        IsAddDialogOpen = false;
    }

    [RelayCommand]
    private void SetFilter(string? filter)
    {
        if (IsSettingsOpen)
        {
            CloseSettingsInternal();
        }

        SelectedFilter = filter switch
        {
            "active" or "completed" or "paused" or "error" => filter,
            _ => "all"
        };

        // ToggleButton can uncheck itself on a second click even with a one-way
        // binding; reassert the derived flags so the active nav item stays lit.
        OnPropertyChanged(nameof(IsFilterAll));
        OnPropertyChanged(nameof(IsFilterActive));
        OnPropertyChanged(nameof(IsFilterCompleted));
        OnPropertyChanged(nameof(IsFilterPaused));
        OnPropertyChanged(nameof(IsFilterError));
    }

    [RelayCommand]
    private void SetSort(string? mode)
    {
        SortMode = mode switch
        {
            "name" or "progress" or "size" or "speed" or "ratio" => mode,
            _ => "added"
        };
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    [RelayCommand]
    private void ResetFilters()
    {
        SearchText = string.Empty;
        SelectedFilter = "all";
    }

    [RelayCommand]
    private void ToggleCompactView()
    {
        IsCompactView = !IsCompactView;
        PersistUiStateLater();
    }

    [RelayCommand]
    private void ToggleDetailsPane()
    {
        IsDetailsPaneOpen = !IsDetailsPaneOpen;
        if (IsDetailsPaneOpen && SelectedTorrent is null && _torrents.Count > 0)
        {
            SelectedTorrent = _torrents[0];
        }

        OnPropertyChanged(nameof(ShowDetailsPane));
        PersistUiStateLater();
    }

    [RelayCommand]
    private async Task PauseTorrentAsync(TorrentItemViewModel? item)
    {
        if (item is null || IsDemoMode)
        {
            return;
        }

        try
        {
            await _engine.PauseAsync(item.Id);
        }
        catch (Exception ex)
        {
            ShowToast(L.T("Could not pause the torrent"), ex.Message, ToastKind.Error);
        }
    }

    [RelayCommand]
    private async Task ResumeTorrentAsync(TorrentItemViewModel? item)
    {
        if (item is null || IsDemoMode)
        {
            return;
        }

        try
        {
            await _engine.ResumeAsync(item.Id);
        }
        catch (Exception ex)
        {
            ShowToast(L.T("Could not resume the torrent"), ex.Message, ToastKind.Error);
        }
    }

    [RelayCommand]
    private async Task TogglePauseSelectedAsync()
    {
        var item = SelectedTorrent;
        if (item is null)
        {
            return;
        }

        if (item.IsPaused)
        {
            await ResumeTorrentAsync(item);
        }
        else
        {
            await PauseTorrentAsync(item);
        }
    }

    [RelayCommand]
    private async Task PauseAllAsync()
    {
        if (IsDemoMode)
        {
            return;
        }

        try
        {
            await _engine.PauseAllAsync();
            ShowToast(L.T("All downloads paused"), L.T("The network is idle; progress is saved."), ToastKind.Info);
        }
        catch (Exception ex)
        {
            ShowToast(L.T("Could not pause downloads"), ex.Message, ToastKind.Error);
        }
    }

    [RelayCommand]
    private async Task ResumeAllAsync()
    {
        if (IsDemoMode)
        {
            return;
        }

        try
        {
            await _engine.ResumeAllAsync();
            ShowToast(L.T("Downloads resumed"), L.T("The client is exchanging data with the swarm again."), ToastKind.Success);
        }
        catch (Exception ex)
        {
            ShowToast(L.T("Could not resume downloads"), ex.Message, ToastKind.Error);
        }
    }

    [RelayCommand]
    private void RequestRemove(TorrentItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        ConfirmRemove = new ConfirmRemoveViewModel(
            item.Id, item.Name, item.TotalSize, item.FileCount);
        IsConfirmRemoveOpen = true;
    }

    [RelayCommand]
    private async Task ConfirmRemoveAsync()
    {
        var confirmation = ConfirmRemove;
        if (confirmation is null)
        {
            return;
        }

        IsConfirmRemoveOpen = false;
        if (IsDemoMode)
        {
            RemoveDemoItem(confirmation.Id);
            ConfirmRemove = null;
            return;
        }

        try
        {
            await _engine.RemoveAsync(confirmation.Id, confirmation.DeleteData);
            ShowToast(
                L.T("Torrent removed"),
                confirmation.DeleteData
                    ? L.T("The data was deleted from disk.")
                    : L.T("Downloaded files were kept."),
                ToastKind.Success);
            if (SelectedTorrent?.Id.Equals(confirmation.Id, StringComparison.OrdinalIgnoreCase) == true)
            {
                SelectedTorrent = null;
            }
        }
        catch (Exception ex)
        {
            ShowToast(L.T("Could not remove the torrent"), ex.Message, ToastKind.Error);
        }
        finally
        {
            ConfirmRemove = null;
        }
    }

    [RelayCommand]
    private void CancelRemove()
    {
        IsConfirmRemoveOpen = false;
        ConfirmRemove = null;
    }

    [RelayCommand]
    private void OpenTorrentFolder(TorrentItemViewModel? item)
    {
        if (item is not null)
        {
            _dialogs.OpenFolder(item.SavePath);
        }
    }

    [RelayCommand]
    private void CopyInfoHash(TorrentItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            Clipboard.SetText(item.InfoHash);
            ShowToast(L.T("Info hash copied"), item.HashShort, ToastKind.Info);
        }
        catch (Exception)
        {
            // Clipboard may be locked by another process.
        }
    }

    [RelayCommand]
    private async Task CheckForUpdates()
    {
        if (_pendingUpdate is not null)
        {
            await InstallPendingUpdateAsync();
            return;
        }

        await CheckForUpdatesAsync(automatic: false);
    }

    private async Task CheckForUpdatesAsync(bool automatic)
    {
        if (IsDemoMode || _updateCheckInFlight)
        {
            return;
        }

        _updateCheckInFlight = true;
        UpdateButtonText = L.T("Checking for updates...");
        UpdateStatusText = L.T("Checking for updates...");
        try
        {
            var info = await _updates.CheckAsync();
            _settings.LastUpdateCheckUtc = DateTime.UtcNow;
            try
            {
                await _settingsService.SaveAsync(_settings);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not persist the last update check time.");
            }

            if (info is null)
            {
                _pendingUpdate = null;
                UpdateButtonText = L.T("Check for updates");
                UpdateStatusText = L.T("You are up to date");
                OnPropertyChanged(nameof(UpdateButtonText));
                if (!automatic)
                {
                    ShowToast(L.T("Updates"), L.T("You are up to date"), ToastKind.Success);
                }

                return;
            }

            _pendingUpdate = info;
            UpdateButtonText = L.F("Update to {0}", info.Version.ToString(3));
            UpdateStatusText = L.F("VibeTorrent {0} is available", info.Version.ToString(3));
            OnPropertyChanged(nameof(UpdateButtonText));
            ShowToast(
                L.T("Update available"),
                L.F("VibeTorrent {0} is available", info.Version.ToString(3)),
                ToastKind.Info);

            if (_settings.AutoInstallUpdates)
            {
                await InstallPendingUpdateAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed.");
            UpdateButtonText = _pendingUpdate is null
                ? L.T("Check for updates")
                : L.F("Update to {0}", _pendingUpdate.Version.ToString(3));
            UpdateStatusText = L.T("Could not check for updates");
            if (!automatic)
            {
                ShowToast(L.T("Updates"), ex.Message, ToastKind.Warning);
            }
        }
        finally
        {
            _updateCheckInFlight = false;
        }
    }

    private async Task InstallPendingUpdateAsync()
    {
        if (_pendingUpdate is null || _updateDownloadInFlight)
        {
            return;
        }

        _updateDownloadInFlight = true;
        try
        {
            UpdateStatusText = L.T("Downloading update...");
            await PersistSessionForShutdownAsync();
            var installer = await _updates.DownloadAsync(_pendingUpdate);
            if (!_updates.LaunchInstaller(installer))
            {
                throw new InvalidOperationException(L.T("Could not start the update installer."));
            }

            UpdateStatusText = L.T("Installing update...");
            ShowToast(L.T("Installing update"), L.T("VibeTorrent will restart automatically."), ToastKind.Info);
            await Task.Delay(350);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update installation failed.");
            UpdateStatusText = L.T("Could not install the update");
            ShowToast(L.T("Updates"), ex.Message, ToastKind.Error);
        }
        finally
        {
            _updateDownloadInFlight = false;
        }
    }

    private void RestartUpdateTimer()
    {
        _updateTimer.Stop();
        if (IsDemoMode || !_settings.AutoCheckUpdates)
        {
            return;
        }

        var hours = Math.Clamp(_settings.UpdateCheckIntervalHours, 1, 720);
        _updateTimer.Interval = TimeSpan.FromHours(hours);
        _updateTimer.Start();
    }

    private async Task RunInitialUpdateCheckAsync()
    {
        if (IsDemoMode || !_settings.AutoCheckUpdates)
        {
            return;
        }

        var due = _settings.LastUpdateCheckUtc is null ||
                  DateTime.UtcNow - _settings.LastUpdateCheckUtc.Value >=
                  TimeSpan.FromHours(Math.Clamp(_settings.UpdateCheckIntervalHours, 1, 720));
        if (!due)
        {
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            if (!_disposed)
            {
                await CheckForUpdatesAsync(automatic: true);
            }
        }
        catch (Exception)
        {
            // The app may be closing; an update check must never crash shutdown.
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        if (IsSettingsOpen && SettingsPage is not null)
        {
            OnPropertyChanged(nameof(IsSettingsOpen));
            return;
        }

        IsSettingsOpen = true;
        AddDialog?.Dispose();
        AddDialog = null;
        IsAddDialogOpen = false;
        SettingsPage = new SettingsViewModel(_settings, _paths, _dialogs, CloseSettingsInternal);
        SettingsPage.Changed += OnSettingsChanged;
        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(HeaderEyebrow));
        OnPropertyChanged(nameof(HeaderSubtitle));
    }

    [RelayCommand]
    private void CloseSettings()
    {
        if (SettingsPage?.IsSaving == true)
        {
            return;
        }

        CloseSettingsInternal();
    }

    private void CloseSettingsInternal()
    {
        _settingsSaveTimer.Stop();
        var page = SettingsPage;
        if (page is not null)
        {
            page.Changed -= OnSettingsChanged;
            _ = SaveSettingsPageAsync(page);
        }

        SettingsPage = null;
        IsSettingsOpen = false;
        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(HeaderEyebrow));
        OnPropertyChanged(nameof(HeaderSubtitle));
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        _settingsSaveTimer.Stop();
        _settingsSaveTimer.Start();
    }

    private async Task SaveSettingsPageAsync(SettingsViewModel? page)
    {
        if (page is null || page.IsSaving || _settingsSaveInFlight)
        {
            return;
        }

        if (IsDemoMode)
        {
            page.ErrorMessage = L.T("Settings are not saved in demo mode.");
            return;
        }

        if (!page.TryBuild(out var candidate, out _))
        {
            return;
        }

        // Window geometry and view state are owned by the shell, not by the
        // settings page. Keep the live values when a debounced save runs.
        candidate.WindowWidth = _settings.WindowWidth;
        candidate.WindowHeight = _settings.WindowHeight;
        candidate.IsWindowMaximized = _settings.IsWindowMaximized;
        candidate.IsDetailsPaneOpen = IsDetailsPaneOpen;
        candidate.CompactList = IsCompactView;
        candidate.SortMode = SortMode;
        candidate.LastFilter = SelectedFilter;
        candidate.LastUpdateCheckUtc = _settings.LastUpdateCheckUtc;

        _settingsSaveInFlight = true;
        page.IsSaving = true;
        try
        {
            candidate.LastUpdateCheckUtc = _settings.LastUpdateCheckUtc;
            await _engine.ApplySettingsAsync(candidate);
            await _settingsService.SaveAsync(candidate);
            _settings = candidate.Clone();
            CurrentSettings = _settings;
            IsDetailsPaneOpen = _settings.IsDetailsPaneOpen;
            IsCompactView = _settings.CompactList;
            RestartUpdateTimer();
            ThemeService.Apply(_settings.Theme);
            LocalizationManager.Instance.SetLanguage(_settings.Language);

            if (!_startupService.TrySetEnabled(candidate.StartWithWindows, out var startupError) &&
                !string.IsNullOrWhiteSpace(startupError))
            {
                ShowToast(L.T("Autostart"), startupError, ToastKind.Warning);
            }
        }
        catch (Exception ex)
        {
            page.ErrorMessage = ex.Message;
        }
        finally
        {
            page.IsSaving = false;
            _settingsSaveInFlight = false;
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        var page = SettingsPage;
        if (page is null)
        {
            return;
        }

        if (!page.TryBuild(out var candidate, out _))
        {
            return;
        }

        if (IsDemoMode)
        {
            CloseSettingsInternal();
            ShowToast(L.T("Demo mode"), L.T("Settings are not saved in demo mode."), ToastKind.Info);
            return;
        }

        page.IsSaving = true;
        page.ErrorMessage = null;
        try
        {
            await _engine.ApplySettingsAsync(candidate);
            await _settingsService.SaveAsync(candidate);
            _settings = candidate.Clone();
            CurrentSettings = _settings;
            IsDetailsPaneOpen = _settings.IsDetailsPaneOpen;
            RestartUpdateTimer();
            ThemeService.Apply(_settings.Theme);
            LocalizationManager.Instance.SetLanguage(_settings.Language);

            if (!_startupService.TrySetEnabled(candidate.StartWithWindows, out var startupError) &&
                !string.IsNullOrWhiteSpace(startupError))
            {
                ShowToast(L.T("Autostart"), startupError, ToastKind.Warning);
            }

            CloseSettingsInternal();
            var warning = _engine.CurrentSnapshot.Stats.Warning;
            ShowToast(
                L.T("Settings saved"),
                string.IsNullOrWhiteSpace(warning)
                    ? L.T("Changes are applied to the current session.")
                    : warning,
                string.IsNullOrWhiteSpace(warning) ? ToastKind.Success : ToastKind.Warning);
        }
        catch (Exception ex)
        {
            page.ErrorMessage = ex.Message;
        }
        finally
        {
            page.IsSaving = false;
        }
    }

    [RelayCommand]
    private void DismissToast(ToastViewModel? toast)
    {
        if (toast is not null)
        {
            Toasts.Remove(toast);
        }
    }

    [RelayCommand]
    private async Task ToggleFileSelectionAsync(TorrentFileSnapshot? file)
    {
        if (file is null || SelectedTorrent is null || IsDemoMode)
        {
            return;
        }

        var next = file.IsSelected ? FilePriority.DoNotDownload : FilePriority.Normal;
        try
        {
            await _engine.SetFilePriorityAsync(SelectedTorrent.Id, file.RelativePath, next);
            await RefreshDetailsAsync(SelectedTorrent.Id);
        }
        catch (Exception ex)
        {
            ShowToast(L.T("Could not change the file priority"), ex.Message, ToastKind.Error);
        }
    }

    [RelayCommand]
    private async Task SetFilePriorityAsync(TorrentFileSnapshot? file)
    {
        if (file is null || SelectedTorrent is null || IsDemoMode)
        {
            return;
        }

        var priority = file.Priority switch
        {
            FilePriority.Normal => FilePriority.High,
            FilePriority.High => FilePriority.DoNotDownload,
            _ => FilePriority.Normal
        };

        try
        {
            await _engine.SetFilePriorityAsync(SelectedTorrent.Id, file.RelativePath, priority);
            await RefreshDetailsAsync(SelectedTorrent.Id);
        }
        catch (Exception ex)
        {
            ShowToast(L.T("Could not change the file priority"), ex.Message, ToastKind.Error);
        }
    }

    [RelayCommand]
    private async Task SetTorrentPriorityAsync(string? value)
    {
        if (SelectedTorrent is null || IsDemoMode ||
            !Enum.TryParse<TorrentPriority>(value, ignoreCase: true, out var priority))
        {
            return;
        }

        try
        {
            var snapshot = await _engine.SetTorrentPriorityAsync(SelectedTorrent.Id, priority);
            SelectedTorrent.Update(snapshot);
            await RefreshDetailsAsync(SelectedTorrent.Id);
        }
        catch (Exception ex)
        {
            ShowToast(L.T("Could not change the torrent priority"), ex.Message, ToastKind.Error);
        }
    }

    [RelayCommand]
    private async Task ApplyTorrentLimitsAsync()
    {
        if (SelectedTorrent is null || IsDemoMode)
        {
            return;
        }

        if (!int.TryParse(SelectedDownloadLimitText, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var download) ||
            !int.TryParse(SelectedUploadLimitText, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var upload) ||
            download is < 0 or > 10_000_000 ||
            upload is < 0 or > 10_000_000)
        {
            ShowToast(L.T("Invalid speed limit"),
                L.T("Use 0 for unlimited or a value between 1 and 10,000,000 KB/s."),
                ToastKind.Warning);
            return;
        }

        try
        {
            var snapshot = await _engine.SetTorrentLimitsAsync(SelectedTorrent.Id, download, upload);
            SelectedTorrent.Update(snapshot);
            await RefreshDetailsAsync(SelectedTorrent.Id);
            ShowToast(L.T("Speed limits updated"), SelectedTorrent.Name, ToastKind.Success);
        }
        catch (Exception ex)
        {
            ShowToast(L.T("Could not update speed limits"), ex.Message, ToastKind.Error);
        }
    }

    [RelayCommand]
    private void OpenDetailsFolder()
    {
        if (SelectedTorrent is not null)
        {
            _dialogs.OpenFolder(SelectedTorrent.SavePath);
        }
    }

    [RelayCommand]
    private async Task ChangeDownloadFolder()
    {
        if (IsDemoMode)
        {
            ShowToast(L.T("Download folder"), L.T("Settings are not saved in demo mode."), ToastKind.Info);
            return;
        }

        var folder = _dialogs.PickFolder(_settings.DownloadDirectory);
        if (string.IsNullOrWhiteSpace(folder) ||
            string.Equals(Path.GetFullPath(folder), Path.GetFullPath(_settings.DownloadDirectory),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var previous = _settings.DownloadDirectory;
        try
        {
            _settings.DownloadDirectory = Path.GetFullPath(folder);
            await _engine.ApplySettingsAsync(_settings);
            await _settingsService.SaveAsync(_settings);
            if (SettingsPage is not null)
            {
                SettingsPage.DownloadDirectory = _settings.DownloadDirectory;
            }

            RefreshStorage();
            ShowToast(L.T("Download folder changed"), _settings.DownloadDirectory, ToastKind.Success);
        }
        catch (Exception ex)
        {
            _settings.DownloadDirectory = previous;
            _logger.LogWarning(ex, "Could not change the download folder.");
            ShowToast(L.T("Download folder"), ex.Message, ToastKind.Error);
        }
    }

    [RelayCommand]
    private async Task MoveDownloadFolderAsync()
    {
        var item = SelectedTorrent;
        if (item is null || IsDemoMode)
        {
            return;
        }

        var target = _dialogs.PickFolder(item.SavePath);
        if (string.IsNullOrWhiteSpace(target) ||
            string.Equals(Path.GetFullPath(target), Path.GetFullPath(item.SavePath),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var snapshot = await _engine.MoveDownloadFolderAsync(item.Id, target);
            item.Update(snapshot);
            await RememberDownloadPathAsync(target);
            await RefreshDetailsAsync(item.Id);
            ShowToast(L.T("Download folder moved"), target, ToastKind.Success);
        }
        catch (Exception ex)
        {
            ShowToast(L.T("Could not move the download folder"), ex.Message, ToastKind.Error);
        }
    }

    [RelayCommand]
    private void CopySavePath()
    {
        if (SelectedTorrent is null)
        {
            return;
        }

        try
        {
            Clipboard.SetText(SelectedTorrent.SavePath);
            ShowToast(L.T("Path copied"), SelectedTorrent.SavePath, ToastKind.Info);
        }
        catch (Exception)
        {
            // Clipboard busy.
        }
    }

    [RelayCommand]
    private async Task RefreshDetailsCommandAsync()
    {
        if (SelectedTorrent is not null)
        {
            await RefreshDetailsAsync(SelectedTorrent.Id);
        }
    }

    public async Task SaveUiStateAsync(double width, double height, bool maximized)
    {
        try
        {
            _settings.WindowWidth = width;
            _settings.WindowHeight = height;
            _settings.IsWindowMaximized = maximized;
            _settings.IsDetailsPaneOpen = IsDetailsPaneOpen;
        _settings.CompactList = IsCompactView;
            _settings.SortMode = SortMode;
            _settings.LastFilter = SelectedFilter;
            await _settingsService.SaveAsync(_settings);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not persist UI state.");
        }
    }

    public async Task PersistSessionForShutdownAsync()
    {
        if (IsDemoMode || _disposed || !_engine.IsInitialized)
        {
            return;
        }

        try
        {
            await _engine.SaveSessionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not persist session for system shutdown.");
        }
    }

    public async Task ShutdownAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _storageTimer.Stop();
        _updateTimer.Stop();
        _detailsCts?.Cancel();
        _detailsCts?.Dispose();
        _detailsCts = null;
        _engine.SnapshotUpdated -= OnEngineSnapshot;

        try
        {
            if (!IsDemoMode)
            {
                await _engine.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Engine shutdown failed.");
        }
    }

    public void Dispose()
    {
        _storageTimer.Stop();
        _updateTimer.Stop();
        _detailsCts?.Cancel();
        _detailsCts?.Dispose();
        _engine.SnapshotUpdated -= OnEngineSnapshot;
        LocalizationManager.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        _ui.Post(RefreshLocalization);
    }

    public void RefreshLocalization()
    {
        UpdateButtonText = _pendingUpdate is null
            ? L.T("Check for updates")
            : L.F("Update to {0}", _pendingUpdate.Version.ToString(3));
        OnPropertyChanged(string.Empty);
        foreach (var item in _torrents)
        {
            item.RefreshLocalization();
        }

        Details?.RefreshLocalization();
    }

    private void OpenAddDialog(AddMode mode)
    {
        if (AddDialog?.IsBusy == true)
        {
            return;
        }

        AddDialog?.Dispose();
        AddDialog = new AddTorrentViewModel(
            _previewService,
            _dialogs,
            _settings.DownloadDirectory,
            SubmitAddAsync,
            CloseAddDialogInternal,
            mode);
        IsAddDialogOpen = true;
        CloseSettingsInternal();
    }

    private async Task OpenAddDialogForFileAsync(string path)
    {
        if (AddDialog?.IsBusy == true)
        {
            return;
        }

        OpenAddDialog(AddMode.File);
        if (AddDialog is not null)
        {
            await AddDialog.LoadFileAsync(path);
        }
    }

    private void CloseAddDialogInternal()
    {
        AddDialog?.Dispose();
        AddDialog = null;
        IsAddDialogOpen = false;
    }

    private async Task RememberDownloadPathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return;
        }

        if (string.Equals(_settings.DownloadDirectory, fullPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // A settings autosave may still be finishing after the add dialog closed.
        // Let it commit first so the newly chosen download folder wins.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (_settingsSaveInFlight && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }

        _settings.DownloadDirectory = fullPath;
        try
        {
            await _settingsService.SaveAsync(_settings);
            RefreshStorage();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist the last download folder.");
        }
    }

    private async Task<(bool Success, string? Error)> SubmitAddAsync(AddTorrentRequest request)
    {
        if (IsDemoMode)
        {
            return (true, null);
        }

        try
        {
            TorrentSnapshot snapshot;
            if (!string.IsNullOrWhiteSpace(request.Magnet))
            {
                snapshot = await _engine.AddMagnetAsync(
                    request.Magnet,
                    new AddTorrentOptions
                    {
                        SavePath = request.SavePath,
                        StartImmediately = request.StartImmediately,
                        FilePriorities = request.Priorities
                    });
            }
            else
            {
                snapshot = await _engine.AddTorrentFileAsync(
                    request.FilePath!,
                    new AddTorrentOptions
                    {
                        SavePath = request.SavePath,
                        StartImmediately = request.StartImmediately,
                        FilePriorities = request.Priorities
                    });
            }

            await RememberDownloadPathAsync(request.SavePath);
            SelectedFilter = "all";
            SearchText = string.Empty;
            var item = _index.TryGetValue(snapshot.Id, out var existing)
                ? existing
                : new TorrentItemViewModel(snapshot);
            if (!_index.ContainsKey(snapshot.Id))
            {
                _index[snapshot.Id] = item;
                _torrents.Insert(0, item);
            }
            else
            {
                item.Update(snapshot);
            }

            TorrentsView.Refresh();
            SelectedTorrent = item;
            IsDetailsPaneOpen = true;
            ShowToast(L.T("Torrent added"), item.Name, ToastKind.Success);
            return (true, null);
        }
        catch (Exception ex) when (ex is TorrentValidationException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return (false, ex.Message);
        }
    }

    private async Task AddTorrentDirectlyAsync(string path, string? magnet)
    {
        var result = await SubmitAddAsync(new AddTorrentRequest(
            magnet is null ? path : null,
            magnet,
            _settings.DownloadDirectory,
            true,
            null));
        if (!result.Success)
        {
            ShowToast(L.T("Could not add the torrent"), result.Error ?? L.T("Unknown error"), ToastKind.Error);
        }
    }

    private async Task ProcessStartupArgumentsAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        // A .torrent opened from Explorer should behave exactly like the
        // "Choose .torrent" button: show the add dialog so the user can pick the
        // download folder, files and start mode. Silent addition bypassed that.
        foreach (var argument in args)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(argument) || argument.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            if (argument.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
            {
                OpenAddDialog(AddMode.Magnet);
                if (AddDialog is not null)
                {
                    AddDialog.MagnetText = argument;
                }

                return;
            }

            if (argument.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase) && File.Exists(argument))
            {
                await OpenAddDialogForFileAsync(argument);
                return;
            }
        }
    }

    private void OnEngineSnapshot(object? sender, EngineSnapshotEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        _ui.Post(() => ApplySnapshot(e.Snapshot));
    }

    private void ApplySnapshot(EngineSnapshot snapshot)
    {
        if (_disposed)
        {
            return;
        }

        var isFirst = !_hasSeenSnapshot;
        var incomingIds = new HashSet<string>(snapshot.Torrents.Select(t => t.Id), StringComparer.OrdinalIgnoreCase);

        foreach (var torrent in snapshot.Torrents)
        {
            if (_index.TryGetValue(torrent.Id, out var existing))
            {
                var wasComplete = _previousComplete.TryGetValue(torrent.Id, out var previousComplete) && previousComplete;
                var previousStatus = _previousStatus.TryGetValue(torrent.Id, out var state) ? state : (TorrentStatus?)null;
                existing.Update(torrent);
                if (!isFirst && !_suppressNotifications)
                {
                    if (!wasComplete && torrent.IsComplete)
                    {
                        NotifyCompletion(existing);
                    }
                    else if (previousStatus is not TorrentStatus.Error && torrent.Status == TorrentStatus.Error)
                    {
                        NotifyError(existing);
                    }
                }
            }
            else
            {
                var item = new TorrentItemViewModel(torrent);
                _index[torrent.Id] = item;
                _torrents.Insert(FindInsertIndex(torrent.AddedAt), item);
            }

            _previousComplete[torrent.Id] = torrent.IsComplete;
            _previousStatus[torrent.Id] = torrent.Status;
        }

        var removed = _index.Keys.Where(id => !incomingIds.Contains(id)).ToArray();
        foreach (var id in removed)
        {
            if (_index.Remove(id, out var item))
            {
                _torrents.Remove(item);
                _previousComplete.Remove(id);
                _previousStatus.Remove(id);
            }

            if (SelectedTorrent?.Id.Equals(id, StringComparison.OrdinalIgnoreCase) == true)
            {
                SelectedTorrent = null;
            }
        }

        _hasSeenSnapshot = true;
        Stats = snapshot.Stats;
        PushHistory(snapshot.Stats);
        OnPropertyChanged(nameof(AllCount));
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(CompletedCount));
        OnPropertyChanged(nameof(PausedCount));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(FilteredCount));
        OnPropertyChanged(nameof(ActiveCaption));
        OnPropertyChanged(nameof(HeaderSubtitle));
        OnPropertyChanged(nameof(FilterSummaryText));
        OnPropertyChanged(nameof(HasAnyTorrents));
        OnPropertyChanged(nameof(IsLibraryEmpty));
        OnPropertyChanged(nameof(IsFilterEmpty));
        OnPropertyChanged(nameof(IsSearchEmpty));
        OnPropertyChanged(nameof(ShowTorrentList));
        OnPropertyChanged(nameof(ShowCardList));
        OnPropertyChanged(nameof(ShowCompactList));

        if (SelectedFilter != "all" || SearchText.Length > 0)
        {
            TorrentsView.Refresh();
        }

        if (SelectedTorrent is null && _torrents.Count > 0 && IsDetailsPaneOpen && !IsLoading)
        {
            SelectedTorrent = _torrents[0];
        }

        EvaluateCompletionAction();
    }

    private void EvaluateCompletionAction()
    {
        var active = ActiveDownloadCount;
        if (active > 0)
        {
            _completionActionArmed = true;
            _completionActionRaised = false;
            return;
        }

        if (!_completionActionArmed || _completionActionRaised || _torrents.Count == 0)
        {
            return;
        }

        // Only fire after a real download phase, never when the user paused or
        // an error left the queue incomplete.
        if (_torrents.Any(t => !t.IsComplete || t.IsError))
        {
            return;
        }

        var action = _settings.AfterDownloadsComplete;
        if (string.IsNullOrWhiteSpace(action) || action == "none")
        {
            _completionActionArmed = false;
            return;
        }

        _completionActionRaised = true;
        _completionActionArmed = false;
        AllDownloadsCompleted?.Invoke(action);
    }

    public void NotifyPowerActionFailed(string message)
    {
        ShowToast(L.T("Power action failed"), message, ToastKind.Error);
    }

    private void PushHistory(SessionStatsSnapshot stats)
    {
        _downloadSamplesQueue.Enqueue(stats.DownloadSpeed);
        _uploadSamplesQueue.Enqueue(stats.UploadSpeed);
        while (_downloadSamplesQueue.Count > HistoryLength)
        {
            _downloadSamplesQueue.Dequeue();
        }

        while (_uploadSamplesQueue.Count > HistoryLength)
        {
            _uploadSamplesQueue.Dequeue();
        }

        DownloadHistory = _downloadSamplesQueue.ToArray();
        UploadHistory = _uploadSamplesQueue.ToArray();
        OnPropertyChanged(nameof(DownloadHistoryMaximum));
        OnPropertyChanged(nameof(UploadHistoryMaximum));
    }

    private int FindInsertIndex(DateTime addedAt)
    {
        for (var i = 0; i < _torrents.Count; i++)
        {
            if (_torrents[i].AddedAt < addedAt)
            {
                return i;
            }
        }

        return _torrents.Count;
    }

    private bool FilterTorrent(object item)
    {
        if (item is not TorrentItemViewModel torrent)
        {
            return false;
        }

        if (SearchText.Length > 0)
        {
            var query = SearchText.Trim();
            if (!torrent.Name.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !torrent.InfoHash.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !torrent.SavePath.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return SelectedFilter switch
        {
            "active" => torrent.IsActive,
            "completed" => torrent.IsComplete,
            "paused" => torrent.IsPaused,
            "error" => torrent.IsError,
            _ => true
        };
    }

    private void ApplySort(bool refresh = true)
    {
        using (TorrentsView.DeferRefresh())
        {
            TorrentsView.SortDescriptions.Clear();
            TorrentsView.SortDescriptions.Add(SortMode switch
            {
                "name" => new SortDescription(nameof(TorrentItemViewModel.Name), ListSortDirection.Ascending),
                "progress" => new SortDescription(nameof(TorrentItemViewModel.Progress), ListSortDirection.Descending),
                "size" => new SortDescription(nameof(TorrentItemViewModel.TotalSize), ListSortDirection.Descending),
                "speed" => new SortDescription(nameof(TorrentItemViewModel.DownloadSpeed), ListSortDirection.Descending),
                "ratio" => new SortDescription(nameof(TorrentItemViewModel.Ratio), ListSortDirection.Descending),
                _ => new SortDescription(nameof(TorrentItemViewModel.AddedAt), ListSortDirection.Descending)
            });
        }

        if (refresh)
        {
            OnPropertyChanged(nameof(SortLabel));
        }
    }

    private void PersistUiStateLater()
    {
        _settings.IsDetailsPaneOpen = IsDetailsPaneOpen;
        _settings.SortMode = SortMode;
        _settings.LastFilter = SelectedFilter;
        if (!IsDemoMode)
        {
            _ = PersistUiStateCoreAsync();
        }
    }

    private async Task PersistUiStateCoreAsync()
    {
        try
        {
            await _settingsService.SaveAsync(_settings);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not save UI preferences.");
        }
    }

    private void RefreshStorage()
    {
        try
        {
            var root = Path.GetPathRoot(_settings.DownloadDirectory);
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            var drive = new DriveInfo(root);
            if (!drive.IsReady)
            {
                StorageFreeText = L.T("Unavailable");
                return;
            }

            var used = drive.TotalSize - drive.AvailableFreeSpace;
            StorageUsedPercent = drive.TotalSize > 0 ? used * 100d / drive.TotalSize : 0;
            StorageFreeText = L.F("{0} free", DisplayFormat.Bytes(drive.AvailableFreeSpace));
            StorageTotalText = L.F("of {0}", DisplayFormat.Bytes(drive.TotalSize));
            StoragePathText = CompactPath(_settings.DownloadDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read drive stats.");
            StorageFreeText = L.T("Unavailable");
        }
    }

    private async Task RefreshDetailsAsync(string id)
    {
        if (IsDemoMode)
        {
            var selected = _index.GetValueOrDefault(id);
            if (selected is not null)
            {
                Details ??= new TorrentDetailsViewModel();
                Details.Update(DemoData.CreateDetails(selected.Snapshot));
            }

            return;
        }

        try
        {
            var details = await _engine.GetDetailsAsync(id);
            await _ui.InvokeAsync(() =>
            {
                if (SelectedTorrent?.Id.Equals(id, StringComparison.OrdinalIgnoreCase) == true)
                {
                    Details ??= new TorrentDetailsViewModel();
                    Details.Update(details);
                }
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Details refresh failed for {Id}.", id);
        }
    }

    private void StartDetailsLoop(string id)
    {
        _detailsCts?.Cancel();
        _detailsCts?.Dispose();
        _detailsCts = new CancellationTokenSource();
        var token = _detailsCts.Token;
        _ = DetailsLoopAsync(id, token);
    }

    private async Task DetailsLoopAsync(string id, CancellationToken token)
    {
        try
        {
            await RefreshDetailsAsync(id);
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), token);
                if (SelectedTorrent?.Id.Equals(id, StringComparison.OrdinalIgnoreCase) != true)
                {
                    return;
                }

                await RefreshDetailsAsync(id);
            }
        }
        catch (OperationCanceledException)
        {
            // Selection changed.
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Details loop stopped.");
        }
    }

    private void LoadDemoLibrary()
    {
        _suppressNotifications = true;
        var snapshots = DemoData.CreateSnapshots();
        var stats = BuildDemoStats(snapshots);
        ApplySnapshot(new EngineSnapshot
        {
            Torrents = snapshots,
            Stats = stats,
            SampledAt = DateTime.UtcNow
        });
        IsDetailsPaneOpen = true;
        SelectedTorrent = _torrents.FirstOrDefault(t => t.IsDownloading && t.HasMetadata)
                          ?? _torrents.FirstOrDefault(t => t.IsSeeding)
                          ?? _torrents.FirstOrDefault();
        DownloadHistory = BuildDemoHistory(12_000_000, 22_000_000, 37);
        UploadHistory = BuildDemoHistory(3_000_000, 15_000_000, 53);
        OnPropertyChanged(nameof(DownloadHistoryMaximum));
        OnPropertyChanged(nameof(UploadHistoryMaximum));
        _suppressNotifications = false;
    }

    private void RemoveDemoItem(string id)
    {
        if (_index.Remove(id, out var item))
        {
            _torrents.Remove(item);
            if (SelectedTorrent == item)
            {
                SelectedTorrent = _torrents.FirstOrDefault();
            }

            OnPropertyChanged(nameof(AllCount));
            OnPropertyChanged(nameof(ActiveCount));
            OnPropertyChanged(nameof(CompletedCount));
            OnPropertyChanged(nameof(PausedCount));
            OnPropertyChanged(nameof(ErrorCount));
            OnPropertyChanged(nameof(FilteredCount));
            OnPropertyChanged(nameof(IsLibraryEmpty));
        }
    }

    private static SessionStatsSnapshot BuildDemoStats(IReadOnlyList<TorrentSnapshot> snapshots)
    {
        return new SessionStatsSnapshot
        {
            DownloadSpeed = snapshots.Sum(s => s.DownloadSpeed),
            UploadSpeed = snapshots.Sum(s => s.UploadSpeed),
            DownloadedBytes = 142_800_000_000,
            UploadedBytes = 86_400_000_000,
            TotalTorrents = snapshots.Count,
            ActiveTorrents = snapshots.Count(s => s.Status is TorrentStatus.Downloading or TorrentStatus.Seeding or TorrentStatus.Metadata or TorrentStatus.Checking),
            SeedingTorrents = snapshots.Count(s => s.Status == TorrentStatus.Seeding),
            CompletedTorrents = snapshots.Count(s => s.IsComplete),
            PausedTorrents = snapshots.Count(s => s.Status == TorrentStatus.Paused),
            ErrorTorrents = snapshots.Count(s => s.Status == TorrentStatus.Error),
            IsEngineRunning = true,
            DhtEnabled = true,
            DhtState = L.T("Connected"),
            DhtNodes = 184,
            ListenPort = 55123,
            Uptime = TimeSpan.FromHours(3.7),
            SampledAt = DateTime.UtcNow
        };
    }

    private static double[] BuildDemoHistory(double min, double max, int seed)
    {
        var random = new Random(seed);
        var values = new double[HistoryLength];
        for (var i = 0; i < values.Length; i++)
        {
            var wave = Math.Sin(i / 4.7 + seed) * 0.14 + Math.Sin(i / 11.3) * 0.08;
            var noise = (random.NextDouble() - 0.5) * 0.18;
            values[i] = Math.Max(0, min + (max - min) * (0.55 + wave + noise));
        }

        return values;
    }

    private void NotifyCompletion(TorrentItemViewModel item)
    {
        ShowToast(L.T("Download complete"), item.Name, ToastKind.Success);
        if (_settings.ShowNotifications)
        {
            NotificationRequested?.Invoke(L.T("Download complete"), item.Name, false);
        }
    }

    private void NotifyError(TorrentItemViewModel item)
    {
        ShowToast(L.T("Download error"), item.Name, ToastKind.Error);
        if (_settings.ShowNotifications)
        {
            NotificationRequested?.Invoke(L.T("Download error"), item.Name, true);
        }
    }

    private void ShowToast(string title, string message, ToastKind kind)
    {
        if (!_ui.IsOnUiThread)
        {
            _ui.Post(() => ShowToast(title, message, kind));
            return;
        }

        var toast = new ToastViewModel { Title = title, Message = message, Kind = kind };
        Toasts.Add(toast);
        while (Toasts.Count > 4)
        {
            Toasts.RemoveAt(0);
        }

        _ = RemoveToastAfterDelayAsync(toast);
    }

    private async Task RemoveToastAfterDelayAsync(ToastViewModel toast)
    {
        await Task.Delay(TimeSpan.FromSeconds(5.5));
        _ui.Post(() => Toasts.Remove(toast));
    }

    private static string CompactPath(string path)
    {
        try
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(profile) &&
                path.StartsWith(profile, StringComparison.OrdinalIgnoreCase))
            {
                return "~" + path[profile.Length..];
            }

            return path;
        }
        catch (Exception)
        {
            return path;
        }
    }

    private static string? NormalizeMagnet(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        return trimmed.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase) ? trimmed : null;
    }

    private static string Plural(int count, string one, string few, string many)
    {
        var modulo100 = count % 100;
        var modulo10 = count % 10;
        if (modulo100 is >= 11 and <= 14)
        {
            return many;
        }

        return modulo10 switch
        {
            1 => one,
            2 or 3 or 4 => few,
            _ => many
        };
    }

    partial void OnSelectedTorrentChanged(TorrentItemViewModel? value)
    {
        OnPropertyChanged(nameof(ShowDetailsPane));
        _detailsCts?.Cancel();
        if (value is null)
        {
            Details = null;
            return;
        }

        Details = new TorrentDetailsViewModel();
        SelectedDownloadLimitText = value.DownloadLimitKbps.ToString(CultureInfo.InvariantCulture);
        SelectedUploadLimitText = value.UploadLimitKbps.ToString(CultureInfo.InvariantCulture);
        StartDetailsLoop(value.Id);
    }

    partial void OnSelectedFilterChanged(string value)
    {
        OnPropertyChanged(nameof(IsFilterAll));
        OnPropertyChanged(nameof(IsFilterActive));
        OnPropertyChanged(nameof(IsFilterCompleted));
        OnPropertyChanged(nameof(IsFilterPaused));
        OnPropertyChanged(nameof(IsFilterError));
        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(HeaderSubtitle));
        OnPropertyChanged(nameof(FilteredCount));
        OnPropertyChanged(nameof(IsFilterEmpty));
        OnPropertyChanged(nameof(ShowTorrentList));
        OnPropertyChanged(nameof(ShowCardList));
        OnPropertyChanged(nameof(ShowCompactList));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateDescription));
        TorrentsView.Refresh();
    }

    partial void OnSearchTextChanged(string value)
    {
        TorrentsView.Refresh();
        OnPropertyChanged(nameof(FilteredCount));
        OnPropertyChanged(nameof(FilterSummaryText));
        OnPropertyChanged(nameof(HasSearch));
        OnPropertyChanged(nameof(IsSearchEmpty));
        OnPropertyChanged(nameof(IsFilterEmpty));
        OnPropertyChanged(nameof(ShowTorrentList));
        OnPropertyChanged(nameof(ShowCardList));
        OnPropertyChanged(nameof(ShowCompactList));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateDescription));
    }

    partial void OnSortModeChanged(string value)
    {
        ApplySort();
        OnPropertyChanged(nameof(SortLabel));
    }

    partial void OnIsCompactViewChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowCardList));
        OnPropertyChanged(nameof(ShowCompactList));
    }

    partial void OnIsDetailsPaneOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDetailsPane));
        if (value && SelectedTorrent is null && _torrents.Count > 0)
        {
            SelectedTorrent = _torrents[0];
        }
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowTorrentList));
        OnPropertyChanged(nameof(ShowCardList));
        OnPropertyChanged(nameof(ShowCompactList));
        OnPropertyChanged(nameof(IsLibraryEmpty));
        OnPropertyChanged(nameof(IsFilterEmpty));
    }

    partial void OnIsSettingsOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDetailsPane));
        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(HeaderEyebrow));
        OnPropertyChanged(nameof(HeaderSubtitle));
    }

    partial void OnStatsChanged(SessionStatsSnapshot value)
    {
        OnPropertyChanged(nameof(DownloadSpeedText));
        OnPropertyChanged(nameof(UploadSpeedText));
        OnPropertyChanged(nameof(DownloadedTotalText));
        OnPropertyChanged(nameof(UploadedTotalText));
        OnPropertyChanged(nameof(DownloadedTotalLabel));
        OnPropertyChanged(nameof(UploadedTotalLabel));
        OnPropertyChanged(nameof(DhtStatusText));
        OnPropertyChanged(nameof(ListenPortText));
        OnPropertyChanged(nameof(EngineStatusText));
        UptimeText = FormatUptime(value.Uptime);
        OnPropertyChanged(nameof(UptimeLabel));
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime <= TimeSpan.Zero)
        {
            return "—";
        }

        if (uptime.TotalDays >= 1)
        {
            return L.F("{0} d {1} h", (int)uptime.TotalDays, uptime.Hours);
        }

        return uptime.TotalHours >= 1
            ? L.F("{0} h {1} min", (int)uptime.TotalHours, uptime.Minutes)
            : L.F("{0} min", uptime.Minutes);
    }
}






