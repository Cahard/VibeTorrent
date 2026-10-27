using VibeTorrent.Core.Localization;

namespace VibeTorrent.Core.Models;

public enum TorrentPriority
{
    Low = 0,
    Normal = 1,
    High = 2
}

public enum TorrentStatus
{
    Metadata,
    Queued,
    Checking,
    Downloading,
    Seeding,
    Completed,
    Paused,
    Stopped,
    Error
}

public enum FilePriority
{
    DoNotDownload = 0,
    Low = 1,
    Normal = 2,
    High = 3,
    Maximum = 4
}

public sealed record TorrentSnapshot
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string InfoHash { get; init; }
    public required string SavePath { get; init; }
    public TorrentStatus Status { get; init; }
    public double Progress { get; init; }
    public long TotalSize { get; init; }
    public long SelectedSize { get; init; }
    public long DownloadedBytes { get; init; }
    public long UploadedBytes { get; init; }
    public long DownloadSpeed { get; init; }
    public long UploadSpeed { get; init; }
    public long? EtaSeconds { get; init; }
    public int PeerCount { get; init; }
    public int SeedCount { get; init; }
    public int LeechCount { get; init; }
    public int FileCount { get; init; }
    public double Ratio { get; init; }
    public bool IsPrivate { get; init; }
    public bool HasMetadata { get; init; }
    public bool IsPaused { get; init; }
    public bool IsQueued { get; init; }
    public bool IsComplete { get; init; }
    public TorrentPriority Priority { get; init; } = TorrentPriority.Normal;
    public int DownloadLimitKbps { get; init; }
    public int UploadLimitKbps { get; init; }
    public DateTime AddedAt { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime SampledAt { get; init; }

    public string ShortHash => InfoHash.Length <= 12 ? InfoHash : InfoHash[..12];
}

public sealed record TorrentFileSnapshot
{
    public required string RelativePath { get; init; }
    public required string FullPath { get; init; }
    public long Length { get; init; }
    public double Progress { get; init; }
    public FilePriority Priority { get; init; }
    public bool IsSelected => Priority != FilePriority.DoNotDownload;
    public long DownloadedBytes => (long)(Length * Math.Clamp(Progress, 0, 1));
}

public sealed record TrackerSnapshot
{
    public required string Url { get; init; }
    public int Tier { get; init; }
    public string Status { get; init; } = "unknown";
    public string StatusText { get; init; } = L.T("Waiting");
    public string? Message { get; init; }
    public TimeSpan? TimeSinceAnnounce { get; init; }
    public bool Succeeded { get; init; }
}

public sealed record PeerSnapshot
{
    public required string Address { get; init; }
    public string Client { get; init; } = L.T("Unknown client");
    public string Direction { get; init; } = "—";
    public bool IsSeeder { get; init; }
    public double Progress { get; init; }
    public long DownloadSpeed { get; init; }
    public long UploadSpeed { get; init; }
    public string Encryption { get; init; } = "—";
    public string Flags { get; init; } = string.Empty;
}

public sealed record TorrentDetailsSnapshot
{
    public required TorrentSnapshot Summary { get; init; }
    public IReadOnlyList<TorrentFileSnapshot> Files { get; init; } = [];
    public IReadOnlyList<TrackerSnapshot> Trackers { get; init; } = [];
    public IReadOnlyList<PeerSnapshot> Peers { get; init; } = [];
    public string? Comment { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? CreationDate { get; init; }
    public DateTime SampledAt { get; init; }
}

public sealed record SessionStatsSnapshot
{
    public long DownloadSpeed { get; init; }
    public long UploadSpeed { get; init; }
    public long DownloadedBytes { get; init; }
    public long UploadedBytes { get; init; }
    public int TotalTorrents { get; init; }
    public int ActiveTorrents { get; init; }
    public int SeedingTorrents { get; init; }
    public int CompletedTorrents { get; init; }
    public int PausedTorrents { get; init; }
    public int ErrorTorrents { get; init; }
    public bool IsEngineRunning { get; init; }
    public bool DhtEnabled { get; init; }
    public string DhtState { get; init; } = L.T("Off");
    public int DhtNodes { get; init; }
    public int ListenPort { get; init; }
    public TimeSpan Uptime { get; init; }
    public string? Warning { get; init; }
    public DateTime SampledAt { get; init; }
}

public sealed record EngineSnapshot
{
    public IReadOnlyList<TorrentSnapshot> Torrents { get; init; } = [];
    public required SessionStatsSnapshot Stats { get; init; }
    public DateTime SampledAt { get; init; }
}

public sealed class EngineSnapshotEventArgs : EventArgs
{
    public EngineSnapshotEventArgs(EngineSnapshot snapshot)
    {
        Snapshot = snapshot;
    }

    public EngineSnapshot Snapshot { get; }
}

public sealed record AddTorrentOptions
{
    public string? SavePath { get; init; }
    public bool StartImmediately { get; init; } = true;
    public IReadOnlyDictionary<string, FilePriority>? FilePriorities { get; init; }
    public string? DisplayName { get; init; }
}

public sealed record PreviewFile
{
    public required string Path { get; init; }
    public long Length { get; init; }
    public bool IsSelected { get; init; } = true;
}

public sealed record TorrentPreview
{
    public required string Name { get; init; }
    public required string InfoHash { get; init; }
    public long TotalSize { get; init; }
    public bool IsPrivate { get; init; }
    public bool HasMetadata { get; init; }
    public string? Comment { get; init; }
    public string? CreatedBy { get; init; }
    public IReadOnlyList<PreviewFile> Files { get; init; } = [];
    public IReadOnlyList<string> Trackers { get; init; } = [];
    public string? SourcePath { get; init; }
    public string? Magnet { get; init; }
}

public sealed class TorrentValidationException : Exception
{
    public TorrentValidationException(string message) : base(message)
    {
    }

    public TorrentValidationException(string message, Exception inner) : base(message, inner)
    {
    }
}
