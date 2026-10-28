using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZTorrent.App.Services;
using ZTorrent.Core.Localization;
using ZTorrent.Core.Models;
using ZTorrent.Core.Services;
using ZTorrent.Core.Utilities;

namespace ZTorrent.App.ViewModels;

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

    public string Title => Mode == AddMode.File
        ? L.T("Add torrent")
        : L.T("Add by link");
    public string Subtitle => Mode == AddMode.File
        ? L.T("Choose a .torrent file, review its contents and the download folder.")
        : L.T("Paste a magnet link — metadata will be fetched from the swarm.");
    public string TorrentPathDisplay => string.IsNullOrWhiteSpace(TorrentPath)
        ? L.T("Drop a .torrent file or choose one")
        : TorrentPath;

    public bool IsFileMode => Mode == AddMode.File;
    public bool IsMagnetMode => Mode == AddMode.Magnet;
    public bool HasPreview => Preview is not null;
    public bool HasFiles => Files.Count > 0;
    public bool ShowFiles => IsFileMode && HasFiles;
    public bool IsPrivate => Preview?.IsPrivate == true;
    public string SourceName => Preview?.Name ?? L.T("No file selected yet");
    public string SourceHash => Preview?.InfoHash ?? "—";
    public string TotalSizeText => Preview is null ? "—" : DisplayFormat.Bytes(Preview.TotalSize);
    public string SelectedSizeText => DisplayFormat.Bytes(Files.Where(f => f.IsSelected).Sum(f => f.Length));
    public string SelectedCountText => L.F("{0} of {1}",
        Files.Count(f => f.IsSelected), Files.Count);
    public string PreviewKindText => Preview is null
        ? string.Empty
        : Preview.IsPrivate
            ? L.T("Private torrent · DHT and PEX disabled")
            : Preview.HasMetadata
                ? L.T("Public torrent")
                : L.T("Magnet · metadata not received yet");

    partial void OnTorrentPathChanged(string? value)
    {
        OnPropertyChanged(nameof(TorrentPathDisplay));
    }

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
                ErrorMessage = L.T("The clipboard is empty.");
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
                ErrorMessage = L.T("There is no magnet link on the clipboard.");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = L.F("Could not read the clipboard: {0}", ex.Message);
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
                    ErrorMessage = L.T("Choose a valid .torrent file first.");
                    return;
                }

                if (Files.All(f => !f.IsSelected))
                {
                    ErrorMessage = L.T("Select at least one file to download.");
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
                ErrorMessage = L.T("Choose a download folder.");
                return;
            }

            var result = await _submit(request);
            if (!result.Success)
            {
                ErrorMessage = result.Error ?? L.T("Could not add the torrent.");
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
            ErrorMessage = L.F("Could not add the torrent: {0}", ex.Message);
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

