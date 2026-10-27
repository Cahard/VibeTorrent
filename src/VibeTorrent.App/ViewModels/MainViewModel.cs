using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using VibeTorrent.App.Demo;
using VibeTorrent.App.Services;
using VibeTorrent.Core.Configuration;
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
    private readonly IUiDispatcher _ui;
    private readonly ILogger<MainViewModel> _logger;

    private readonly ObservableCollection<TorrentItemViewModel> _torrents = [];
    private readonly Dictionary<string, TorrentItemViewModel> _index = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _previousComplete = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TorrentStatus> _previousStatus = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<double> _downloadSamplesQueue = new();
    private readonly Queue<double> _uploadSamplesQueue = new();
    private readonly DispatcherTimer _storageTimer;

    private AppSettings _settings = AppSettings.Default;
    private CancellationTokenSource? _detailsCts;
    private bool _suppressNotifications;
    private bool _hasSeenSnapshot;
    private bool _disposed;

    public MainViewModel(
        ITorrentEngineService engine,
        ISettingsService settingsService,
        ITorrentPreviewService previewService,
        AppPaths paths,
        IDialogService dialogs,
        IStartupService startupService,
        IUiDispatcher ui,
        ILogger<MainViewModel> logger)
    {
        _engine = engine;
        _settingsService = settingsService;
        _previewService = previewService;
        _paths = paths;
        _dialogs = dialogs;
        _startupService = startupService;
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
        ? "DHT выключен"
        : $"{Stats.DhtState} · {Stats.DhtNodes:N0} {Plural(Stats.DhtNodes, "узел", "узла", "узлов")}";
    public string ListenPortText => Stats.ListenPort > 0 ? $"Порт {Stats.ListenPort}" : "Порт: авто";
    public string EngineStatusText => Stats.IsEngineRunning ? "Сеть активна" : "Сеть остановлена";
    public string ActiveCaption => $"{ActiveCount} активн{Plural(ActiveCount, "ый", "ых", "ых")}";
    public string HeaderTitle => IsSettingsOpen ? "Настройки" : SelectedFilter switch
    {
        "active" => "Активные",
        "completed" => "Завершённые",
        "paused" => "На паузе",
        "error" => "С ошибкой",
        _ => "Все загрузки"
    };
    public string HeaderEyebrow => IsSettingsOpen ? "КОНФИГУРАЦИЯ" : "БИБЛИОТЕКА";
    public string HeaderSubtitle => IsSettingsOpen
        ? "Сеть, скорость и хранение — всё под контролем."
        : _torrents.Count == 0
            ? "Добавьте первый торрент — файл или magnet-ссылку."
            : $"{AllCount} {Plural(AllCount, "загрузка", "загрузки", "загрузок")} · {ActiveCount} {Plural(ActiveCount, "активна", "активны", "активны")}";
    public string FilterSummaryText => SearchText.Length > 0
        ? $"Найдено: {FilteredCount}"
        : $"{FilteredCount} {Plural(FilteredCount, "торрент", "торрента", "торрентов")}";

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
    public string EmptyStateTitle => IsSearchEmpty ? "Ничего не нашлось" : "Пока тихо";
    public string EmptyStateDescription => IsSearchEmpty
        ? "Попробуйте изменить запрос или сбросить фильтр."
        : "Перетащите .torrent в окно или добавьте magnet-ссылку. Загрузки появятся здесь.";
    public string SortLabel => SortMode switch
    {
        "name" => "По имени",
        "progress" => "По прогрессу",
        "size" => "По размеру",
        "speed" => "По скорости",
        "ratio" => "По рейтингу",
        _ => "Сначала новые"
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
            ShowToast("Не удалось запустить сеть", ex.Message, ToastKind.Error);
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
        if (!string.IsNullOrWhiteSpace(Stats.Warning))
        {
            ShowToast("Сеть", Stats.Warning, ToastKind.Warning);
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
            ShowToast("Не удалось поставить на паузу", ex.Message, ToastKind.Error);
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
            ShowToast("Не удалось возобновить", ex.Message, ToastKind.Error);
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
            ShowToast("Все загрузки на паузе", "Сеть освобождена, позиции сохранены.", ToastKind.Info);
        }
        catch (Exception ex)
        {
            ShowToast("Не удалось остановить загрузки", ex.Message, ToastKind.Error);
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
            ShowToast("Загрузки возобновлены", "Клиент снова обменивается данными с роем.", ToastKind.Success);
        }
        catch (Exception ex)
        {
            ShowToast("Не удалось возобновить загрузки", ex.Message, ToastKind.Error);
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
                "Торрент удалён",
                confirmation.DeleteData ? "Данные также удалены с диска." : "Скачанные файлы сохранены.",
                ToastKind.Success);
            if (SelectedTorrent?.Id.Equals(confirmation.Id, StringComparison.OrdinalIgnoreCase) == true)
            {
                SelectedTorrent = null;
            }
        }
        catch (Exception ex)
        {
            ShowToast("Не удалось удалить торрент", ex.Message, ToastKind.Error);
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
            ShowToast("Info hash скопирован", item.HashShort, ToastKind.Info);
        }
        catch (Exception)
        {
            // Clipboard may be locked by another process.
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
        SettingsPage = null;
        IsSettingsOpen = false;
        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(HeaderEyebrow));
        OnPropertyChanged(nameof(HeaderSubtitle));
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
            ShowToast("Демо-режим", "Настройки не сохраняются в демонстрационном режиме.", ToastKind.Info);
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

            if (!_startupService.TrySetEnabled(candidate.StartWithWindows, out var startupError) &&
                !string.IsNullOrWhiteSpace(startupError))
            {
                ShowToast("Автозапуск", startupError, ToastKind.Warning);
            }

            CloseSettingsInternal();
            var warning = _engine.CurrentSnapshot.Stats.Warning;
            ShowToast(
                "Настройки сохранены",
                string.IsNullOrWhiteSpace(warning) ? "Изменения применены к текущей сессии." : warning,
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
            ShowToast("Не удалось изменить приоритет файла", ex.Message, ToastKind.Error);
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
            ShowToast("Не удалось изменить приоритет файла", ex.Message, ToastKind.Error);
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
    private void CopySavePath()
    {
        if (SelectedTorrent is null)
        {
            return;
        }

        try
        {
            Clipboard.SetText(SelectedTorrent.SavePath);
            ShowToast("Путь скопирован", SelectedTorrent.SavePath, ToastKind.Info);
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
        _detailsCts?.Cancel();
        _detailsCts?.Dispose();
        _engine.SnapshotUpdated -= OnEngineSnapshot;
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
        SettingsPage = null;
        IsSettingsOpen = false;
    }

    private async Task OpenAddDialogForFileAsync(string path)
    {
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
            ShowToast("Торрент добавлен", item.Name, ToastKind.Success);
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
            ShowToast("Не удалось добавить торрент", result.Error ?? "Неизвестная ошибка", ToastKind.Error);
        }
    }

    private async Task ProcessStartupArgumentsAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        foreach (var argument in args)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(argument) || argument.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            if (argument.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
            {
                await AddTorrentDirectlyAsync(string.Empty, argument);
            }
            else if (argument.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase) && File.Exists(argument))
            {
                await AddTorrentDirectlyAsync(argument, null);
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

        if (SelectedFilter != "all" || SearchText.Length > 0)
        {
            TorrentsView.Refresh();
        }

        if (SelectedTorrent is null && _torrents.Count > 0 && IsDetailsPaneOpen && !IsLoading)
        {
            SelectedTorrent = _torrents[0];
        }
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
                StorageFreeText = "Недоступно";
                return;
            }

            var used = drive.TotalSize - drive.AvailableFreeSpace;
            StorageUsedPercent = drive.TotalSize > 0 ? used * 100d / drive.TotalSize : 0;
            StorageFreeText = $"{DisplayFormat.Bytes(drive.AvailableFreeSpace)} свободно";
            StorageTotalText = $"из {DisplayFormat.Bytes(drive.TotalSize)}";
            StoragePathText = CompactPath(_settings.DownloadDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read drive stats.");
            StorageFreeText = "Недоступно";
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
            DhtState = "Подключено",
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
        ShowToast("Загрузка завершена", item.Name, ToastKind.Success);
        if (_settings.ShowNotifications)
        {
            NotificationRequested?.Invoke("Загрузка завершена", item.Name, false);
        }
    }

    private void NotifyError(TorrentItemViewModel item)
    {
        ShowToast("Ошибка загрузки", item.Name, ToastKind.Error);
        if (_settings.ShowNotifications)
        {
            NotificationRequested?.Invoke("Ошибка загрузки", item.Name, true);
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
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateDescription));
    }

    partial void OnSortModeChanged(string value)
    {
        ApplySort();
        OnPropertyChanged(nameof(SortLabel));
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
        OnPropertyChanged(nameof(DhtStatusText));
        OnPropertyChanged(nameof(ListenPortText));
        OnPropertyChanged(nameof(EngineStatusText));
        OnPropertyChanged(nameof(UptimeText));
        UptimeText = FormatUptime(value.Uptime);
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime <= TimeSpan.Zero)
        {
            return "—";
        }

        if (uptime.TotalDays >= 1)
        {
            return $"{(int)uptime.TotalDays} д {uptime.Hours} ч";
        }

        return uptime.TotalHours >= 1
            ? $"{(int)uptime.TotalHours} ч {uptime.Minutes} мин"
            : $"{uptime.Minutes} мин";
    }
}






