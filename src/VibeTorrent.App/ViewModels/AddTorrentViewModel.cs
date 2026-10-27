using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VibeTorrent.App.Services;
using VibeTorrent.Core.Models;
using VibeTorrent.Core.Services;
using VibeTorrent.Core.Utilities;

namespace VibeTorrent.App.ViewModels;

public enum AddMode
{
    File,
    Magnet
}

public sealed record AddTorrentRequest(
    string? FilePath,
    string? Magnet,
    string SavePath,
    bool StartImmediately,
    IReadOnlyDictionary<string, FilePriority>? Priorities);

public sealed partial class AddTorrentViewModel : ObservableObject, IDisposable
{
    private readonly ITorrentPreviewService _previewService;
    private readonly IDialogService _dialogs;
    private readonly Func<AddTorrentRequest, Task<(bool Success, string? Error)>> _submit;
    private readonly Action _close;
    private CancellationTokenSource? _magnetCts;

    public AddTorrentViewModel(
        ITorrentPreviewService previewService,
        IDialogService dialogs,
        string defaultSavePath,
        Func<AddTorrentRequest, Task<(bool Success, string? Error)>> submit,
        Action close,
        AddMode initialMode = AddMode.File)
    {
        _previewService = previewService;
        _dialogs = dialogs;
        _submit = submit;
        _close = close;
        SavePath = defaultSavePath;
        Mode = initialMode;
    }

    public ObservableCollection<SelectableFileViewModel> Files { get; } = [];

    [ObservableProperty]
    private AddMode _mode;

    [ObservableProperty]
    private string? _torrentPath;

    [ObservableProperty]
    private string _magnetText = string.Empty;

    [ObservableProperty]
    private TorrentPreview? _preview;

    [ObservableProperty]
    private string _savePath;

    [ObservableProperty]
    private bool _startImmediately = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public string Title => Mode == AddMode.File ? "Добавить торрент" : "Добавить по ссылке";
    public string Subtitle => Mode == AddMode.File
        ? "Выберите .torrent, проверьте содержимое и папку загрузки."
        : "Вставьте magnet-ссылку — метаданные загрузятся из роя.";
    public bool IsFileMode => Mode == AddMode.File;
    public bool IsMagnetMode => Mode == AddMode.Magnet;
    public bool HasPreview => Preview is not null;
    public bool HasFiles => Files.Count > 0;
    public bool ShowFiles => IsFileMode && HasFiles;
    public bool IsPrivate => Preview?.IsPrivate == true;
    public string SourceName => Preview?.Name ?? "Файл ещё не выбран";
    public string SourceHash => Preview?.InfoHash ?? "—";
    public string TotalSizeText => Preview is null ? "—" : DisplayFormat.Bytes(Preview.TotalSize);
    public string SelectedSizeText => DisplayFormat.Bytes(Files.Where(f => f.IsSelected).Sum(f => f.Length));
    public string SelectedCountText => $"{Files.Count(f => f.IsSelected):N0} из {Files.Count:N0}";
    public string PreviewKindText => Preview is null
        ? string.Empty
        : Preview.IsPrivate
            ? "Приватный торрент · DHT и PEX отключены"
            : Preview.HasMetadata
                ? "Публичный торрент"
                : "Magnet · метаданные ещё не получены";

    partial void OnModeChanged(AddMode value)
    {
        // A preview belongs to one source type; never show a .torrent file list
        // under the magnet tab or vice versa.
        Preview = null;
        ErrorMessage = null;
        if (value == AddMode.Magnet)
        {
            TorrentPath = null;
        }
        else
        {
            MagnetText = string.Empty;
        }

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(IsFileMode));
        OnPropertyChanged(nameof(IsMagnetMode));
        OnPropertyChanged(nameof(ShowFiles));
    }

    partial void OnPreviewChanged(TorrentPreview? value)
    {
        RebuildFiles();
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(IsPrivate));
        OnPropertyChanged(nameof(SourceName));
        OnPropertyChanged(nameof(SourceHash));
        OnPropertyChanged(nameof(TotalSizeText));
        OnPropertyChanged(nameof(PreviewKindText));
        OnPropertyChanged(nameof(SelectedSizeText));
        OnPropertyChanged(nameof(SelectedCountText));
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(ShowFiles));
    }

    partial void OnMagnetTextChanged(string value)
    {
        _magnetCts?.Cancel();
        _magnetCts?.Dispose();
        _magnetCts = new CancellationTokenSource();
        var token = _magnetCts.Token;
        _ = PreviewMagnetDelayedAsync(value, token);
    }

    private async Task PreviewMagnetDelayedAsync(string value, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(450, cancellationToken);
            if (cancellationToken.IsCancellationRequested || Mode != AddMode.Magnet)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                Preview = null;
                return;
            }

            Preview = _previewService.InspectMagnet(value);
            ErrorMessage = null;
        }
        catch (OperationCanceledException)
        {
            // Typing continues.
        }
        catch (TorrentValidationException)
        {
            Preview = null;
        }
        catch (Exception)
        {
            Preview = null;
        }
    }

    public async Task LoadFileAsync(string path)
    {
        Mode = AddMode.File;
        TorrentPath = path;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            Preview = await _previewService.InspectTorrentFileAsync(path);
        }
        catch (Exception ex) when (ex is TorrentValidationException or IOException or UnauthorizedAccessException)
        {
            Preview = null;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var path = _dialogs.PickTorrentFile();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await LoadFileAsync(path);
        }
    }

    [RelayCommand]
    private void SwitchMode(string? mode)
    {
        Mode = mode?.Equals("magnet", StringComparison.OrdinalIgnoreCase) == true
            ? AddMode.Magnet
            : AddMode.File;

        if (Mode == AddMode.File)
        {
            ErrorMessage = null;
        }
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var folder = _dialogs.PickFolder(SavePath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            SavePath = folder;
        }
    }

    [RelayCommand]
    private void PasteMagnet()
    {
        try
        {
            if (!Clipboard.ContainsText())
            {
                ErrorMessage = "Буфер обмена пуст.";
                return;
            }

            var text = Clipboard.GetText().Trim();
            if (text.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
            {
                Mode = AddMode.Magnet;
                MagnetText = text;
                ErrorMessage = null;
            }
            else if (text.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
            {
                _ = LoadFileAsync(text);
            }
            else
            {
                ErrorMessage = "В буфере обмена нет magnet-ссылки.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Не удалось прочитать буфер обмена: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var file in Files)
        {
            file.IsSelected = true;
        }

        UpdateSelectionSummary();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var file in Files)
        {
            file.IsSelected = false;
        }

        UpdateSelectionSummary();
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            AddTorrentRequest request;
            if (Mode == AddMode.File)
            {
                if (Preview is null || string.IsNullOrWhiteSpace(TorrentPath))
                {
                    ErrorMessage = "Сначала выберите корректный .torrent файл.";
                    return;
                }

                if (Files.All(f => !f.IsSelected))
                {
                    ErrorMessage = "Выберите хотя бы один файл для скачивания.";
                    return;
                }

                var priorities = Files.ToDictionary(
                    f => PathSafety.CanonicalRelativePath(f.Path),
                    f => f.IsSelected ? FilePriority.Normal : FilePriority.DoNotDownload,
                    StringComparer.OrdinalIgnoreCase);
                request = new AddTorrentRequest(TorrentPath, null, SavePath, StartImmediately, priorities);
            }
            else
            {
                var magnetPreview = _previewService.InspectMagnet(MagnetText);
                Preview = magnetPreview;
                request = new AddTorrentRequest(null, MagnetText.Trim(), SavePath, StartImmediately, null);
            }

            if (string.IsNullOrWhiteSpace(request.SavePath))
            {
                ErrorMessage = "Укажите папку для загрузки.";
                return;
            }

            var result = await _submit(request);
            if (!result.Success)
            {
                ErrorMessage = result.Error ?? "Не удалось добавить торрент.";
                return;
            }

            _close();
        }
        catch (TorrentValidationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Не удалось добавить торрент: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Close()
    {
        if (!IsBusy)
        {
            _close();
        }
    }

    private void RebuildFiles()
    {
        Files.Clear();
        if (Preview is null)
        {
            return;
        }

        foreach (var file in Preview.Files)
        {
            Files.Add(new SelectableFileViewModel(file, file.IsSelected, UpdateSelectionSummary));
        }

        UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        OnPropertyChanged(nameof(SelectedCountText));
        OnPropertyChanged(nameof(SelectedSizeText));
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(ShowFiles));
        ErrorMessage = null;
    }

    public void Dispose()
    {
        _magnetCts?.Cancel();
        _magnetCts?.Dispose();
        _magnetCts = null;
    }
}

