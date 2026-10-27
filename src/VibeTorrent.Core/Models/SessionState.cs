namespace VibeTorrent.Core.Models;

public sealed class SessionState
{
    public int Version { get; set; } = 1;
    public List<PersistedTorrent> Torrents { get; set; } = [];
}

public sealed class PersistedTorrent
{
    public required string Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TorrentPath { get; set; }
    public string? Magnet { get; set; }
    public required string SavePath { get; set; }
    public bool StartPaused { get; set; }
    public DateTime AddedAt { get; set; }
    public long KnownSize { get; set; }
    public Dictionary<string, FilePriority> FilePriorities { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public double LastKnownProgress { get; set; }
}
