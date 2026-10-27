using CommunityToolkit.Mvvm.ComponentModel;
using VibeTorrent.App.Converters;
using VibeTorrent.Core.Localization;
using VibeTorrent.Core.Models;
using VibeTorrent.Core.Utilities;

namespace VibeTorrent.App.ViewModels;

/// <summary>
/// A stable row object. The list keeps the same instance across one-second
/// snapshots so selection, hover and context menus are never lost.
/// </summary>
public sealed class TorrentItemViewModel : ObservableObject
{
    private TorrentSnapshot _snapshot;

    public TorrentItemViewModel(TorrentSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public TorrentSnapshot Snapshot => _snapshot;

    public string Id => _snapshot.Id;
    public string Name => _snapshot.Name;
    public string InfoHash => _snapshot.InfoHash;
    public string HashShort => _snapshot.ShortHash;
    public string SavePath => _snapshot.SavePath;
    public TorrentStatus Status => _snapshot.Status;
    public string StatusText => StatusTextConverter.StatusText(Status);
    public double Progress => _snapshot.Progress;
    public string ProgressText => DisplayFormat.Percent(_snapshot.Progress);
    public long TotalSize => _snapshot.TotalSize;
    public string SizeText => DisplayFormat.Bytes(_snapshot.TotalSize);
    public long SelectedSize => _snapshot.SelectedSize;
    public string SelectedSizeText => DisplayFormat.Bytes(_snapshot.SelectedSize);
    public long DownloadedBytes => _snapshot.DownloadedBytes;
    public string DownloadedText => DisplayFormat.Bytes(_snapshot.DownloadedBytes);
    public long UploadedBytes => _snapshot.UploadedBytes;
    public long DownloadSpeed => _snapshot.DownloadSpeed;
    public long UploadSpeed => _snapshot.UploadSpeed;
    public string DownloadSpeedText => DisplayFormat.Speed(_snapshot.DownloadSpeed);
    public string UploadSpeedText => DisplayFormat.Speed(_snapshot.UploadSpeed);
    public string EtaText => DisplayFormat.Eta(_snapshot.EtaSeconds);
    public int PeerCount => _snapshot.PeerCount;
    public int SeedCount => _snapshot.SeedCount;
    public int LeechCount => _snapshot.LeechCount;
    public string PeersText => L.F("{0} peers · {1} seeds", _snapshot.PeerCount, _snapshot.SeedCount);
    public int FileCount => _snapshot.FileCount;
    public string FileCountText => L.F("{0} files", _snapshot.FileCount);
    public double Ratio => _snapshot.Ratio;
    public string RatioText => DisplayFormat.Ratio(_snapshot.Ratio);
    public string RatioLabel => L.F("Ratio {0}", RatioText);
    public TorrentPriority Priority => _snapshot.Priority;
    public string PriorityText => Priority switch
    {
        TorrentPriority.High => L.T("High"),
        TorrentPriority.Low => L.T("Low"),
        _ => L.T("Normal")
    };
    public bool IsHighPriority => Priority == TorrentPriority.High;
    public bool IsNormalPriority => Priority == TorrentPriority.Normal;
    public bool IsLowPriority => Priority == TorrentPriority.Low;
    public bool IsQueued => _snapshot.IsQueued;
    public int DownloadLimitKbps => _snapshot.DownloadLimitKbps;
    public int UploadLimitKbps => _snapshot.UploadLimitKbps;
    public bool IsPrivate => _snapshot.IsPrivate;
    public bool HasMetadata => _snapshot.HasMetadata;
    public bool IsPaused => _snapshot.IsPaused || _snapshot.Status == TorrentStatus.Paused;
    public bool IsComplete => _snapshot.IsComplete;
    public bool IsError => _snapshot.Status == TorrentStatus.Error;
    public bool IsDownloading => _snapshot.Status is TorrentStatus.Downloading or TorrentStatus.Metadata or TorrentStatus.Checking;
    public bool IsSeeding => _snapshot.Status == TorrentStatus.Seeding;
    public bool IsActive => _snapshot.Status is TorrentStatus.Downloading or TorrentStatus.Seeding
        or TorrentStatus.Metadata or TorrentStatus.Checking;
    public DateTime AddedAt => _snapshot.AddedAt;
    public string AddedText => DisplayFormat.Date(_snapshot.AddedAt);
    public string? ErrorMessage => _snapshot.ErrorMessage;
    public bool HasError => !string.IsNullOrWhiteSpace(_snapshot.ErrorMessage);
    public string StatusDetail => BuildStatusDetail();

    public void Update(TorrentSnapshot snapshot)
    {
        _snapshot = snapshot;
        OnPropertyChanged(string.Empty);
    }

    public void RefreshLocalization() => OnPropertyChanged(string.Empty);

    private string BuildStatusDetail()
    {
        if (IsError && HasError)
        {
            return ErrorMessage!;
        }

        if (IsComplete && IsSeeding)
        {
            return L.F("Seeding · {0}", PeersText);
        }

        if (IsPaused)
        {
            return L.F("Paused at {0}", DisplayFormat.Percent(Progress, 0));
        }

        if (Status == TorrentStatus.Metadata)
        {
            return L.T("Fetching metadata from the swarm");
        }

        if (Status == TorrentStatus.Checking)
        {
            return L.T("Checking file integrity");
        }

        return L.F("{0} · {1} left", DownloadSpeedText, EtaText);
    }
}
