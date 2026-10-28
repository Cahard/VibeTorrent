using ZTorrent.Core.Localization;
using ZTorrent.Core.Models;

namespace ZTorrent.App.Demo;

/// <summary>
/// Deterministic sample library used only by the --demo / --screenshots flags.
/// It never touches the network or the real session store.
/// </summary>
public static class DemoData
{
    public static IReadOnlyList<TorrentSnapshot> CreateSnapshots()
    {
        var now = DateTime.UtcNow;
        return
        [
            new TorrentSnapshot
            {
                Id = "4f2c8a1d9b3e5f60718293a4b5c6d7e8f9012345",
                Name = "NeuroSymphony — Original Soundtrack [FLAC]",
                InfoHash = "4f2c8a1d9b3e5f60718293a4b5c6d7e8f9012345",
                SavePath = DemoDownloads("NeuroSymphony"),
                Status = TorrentStatus.Downloading,
                Progress = 0.634,
                TotalSize = 4_963_000_000,
                SelectedSize = 4_963_000_000,
                DownloadedBytes = 3_146_000_000,
                UploadedBytes = 672_000_000,
                DownloadSpeed = 19_400_000,
                UploadSpeed = 2_300_000,
                EtaSeconds = 94,
                PeerCount = 42,
                SeedCount = 18,
                LeechCount = 24,
                FileCount = 24,
                Ratio = 0.21,
                IsPrivate = false,
                HasMetadata = true,
                AddedAt = now.AddMinutes(-47),
                SampledAt = now
            },
            new TorrentSnapshot
            {
                Id = "9a7e3c2b1d4f5061728394a5b6c7d8e9f0011223",
                Name = "Cyberpunk 2077: Phantom Liberty — 4K Cinematics",
                InfoHash = "9a7e3c2b1d4f5061728394a5b6c7d8e9f0011223",
                SavePath = DemoVideos("Cyberpunk 2077"),
                Status = TorrentStatus.Seeding,
                Progress = 1,
                TotalSize = 8_724_000_000,
                SelectedSize = 8_724_000_000,
                DownloadedBytes = 8_724_000_000,
                UploadedBytes = 18_600_000_000,
                DownloadSpeed = 0,
                UploadSpeed = 12_600_000,
                PeerCount = 31,
                SeedCount = 22,
                LeechCount = 9,
                FileCount = 12,
                Ratio = 2.14,
                IsPrivate = false,
                HasMetadata = true,
                IsComplete = true,
                AddedAt = now.AddHours(-4),
                SampledAt = now
            },
            new TorrentSnapshot
            {
                Id = "b23d4e5f60718293a4b5c6d7e8f901234567aabb",
                Name = "Ubuntu 25.10 Desktop amd64",
                InfoHash = "b23d4e5f60718293a4b5c6d7e8f901234567aabb",
                SavePath = DemoDownloads("Ubuntu"),
                Status = TorrentStatus.Paused,
                Progress = 0.412,
                TotalSize = 6_120_000_000,
                SelectedSize = 6_120_000_000,
                DownloadedBytes = 2_521_000_000,
                UploadedBytes = 310_000_000,
                PeerCount = 0,
                SeedCount = 0,
                LeechCount = 0,
                FileCount = 4,
                Ratio = 0.12,
                IsPaused = true,
                HasMetadata = true,
                AddedAt = now.AddHours(-2.2),
                SampledAt = now
            },
            new TorrentSnapshot
            {
                Id = "ce5f60718293a4b5c6d7e8f901234567aabbccdd",
                Name = "Blender Studio — Sprite Fright 4K",
                InfoHash = "ce5f60718293a4b5c6d7e8f901234567aabbccdd",
                SavePath = DemoVideos("Blender Studio"),
                Status = TorrentStatus.Completed,
                Progress = 1,
                TotalSize = 2_286_000_000,
                SelectedSize = 2_286_000_000,
                DownloadedBytes = 2_286_000_000,
                UploadedBytes = 2_770_000_000,
                FileCount = 7,
                Ratio = 1.21,
                HasMetadata = true,
                IsComplete = true,
                AddedAt = now.AddDays(-1.4),
                SampledAt = now
            },
            new TorrentSnapshot
            {
                Id = "d60718293a4b5c6d7e8f9012345678aabbccddee",
                Name = "The Midnight — Endless Summer (Deluxe) [FLAC 24/96]",
                InfoHash = "d60718293a4b5c6d7e8f9012345678aabbccddee",
                SavePath = DemoMusic("The Midnight"),
                Status = TorrentStatus.Downloading,
                Progress = 0.126,
                TotalSize = 1_374_000_000,
                SelectedSize = 1_374_000_000,
                DownloadedBytes = 173_000_000,
                DownloadSpeed = 3_100_000,
                UploadSpeed = 140_000,
                EtaSeconds = 388,
                PeerCount = 18,
                SeedCount = 9,
                LeechCount = 9,
                FileCount = 18,
                Ratio = 0.04,
                HasMetadata = true,
                AddedAt = now.AddMinutes(-19),
                SampledAt = now
            },
            new TorrentSnapshot
            {
                Id = "e718293a4b5c6d7e8f9012345678aabbccddeeff",
                Name = "Learn Rust — Complete Course 2026",
                InfoHash = "e718293a4b5c6d7e8f9012345678aabbccddeeff",
                SavePath = DemoDownloads("Rust"),
                Status = TorrentStatus.Metadata,
                TotalSize = 4_400_000_000,
                PeerCount = 7,
                SeedCount = 2,
                LeechCount = 5,
                FileCount = 0,
                HasMetadata = false,
                AddedAt = now.AddMinutes(-2),
                SampledAt = now
            },
            new TorrentSnapshot
            {
                Id = "f8293a4b5c6d7e8f9012345678aabbccddeeff00",
                Name = "NASA Artemis II — Launch Footage (8K Master)",
                InfoHash = "f8293a4b5c6d7e8f9012345678aabbccddeeff00",
                SavePath = DemoVideos("NASA Archive"),
                Status = TorrentStatus.Error,
                Progress = 0.028,
                TotalSize = 20_100_000_000,
                SelectedSize = 20_100_000_000,
                DownloadedBytes = 562_000_000,
                PeerCount = 0,
                SeedCount = 0,
                LeechCount = 0,
                FileCount = 3,
                HasMetadata = true,
                ErrorMessage = L.T("Not enough space on drive D:. Free up 18.2 GB."),
                AddedAt = now.AddHours(-8),
                SampledAt = now
            },
            new TorrentSnapshot
            {
                Id = "a93a4b5c6d7e8f9012345678aabbccddeeff0011",
                Name = "Open Source UI Kit — Figma + Design Tokens",
                InfoHash = "a93a4b5c6d7e8f9012345678aabbccddeeff0011",
                SavePath = DemoDownloads("UI-Kit"),
                Status = TorrentStatus.Seeding,
                Progress = 1,
                TotalSize = 818_000_000,
                SelectedSize = 818_000_000,
                DownloadedBytes = 818_000_000,
                UploadedBytes = 1_600_000_000,
                UploadSpeed = 2_400_000,
                PeerCount = 12,
                SeedCount = 8,
                LeechCount = 4,
                FileCount = 46,
                Ratio = 1.96,
                HasMetadata = true,
                IsComplete = true,
                AddedAt = now.AddDays(-3),
                SampledAt = now
            }
        ];
    }

    public static TorrentDetailsSnapshot CreateDetails(TorrentSnapshot summary)
    {
        var files = new List<TorrentFileSnapshot>();
        var baseName = summary.Name.Length > 28 ? summary.Name[..28] : summary.Name;
        var count = summary.FileCount;
        var total = Math.Max(1, summary.TotalSize);
        for (var i = 0; i < count; i++)
        {
            var ext = (i % 7) switch
            {
                3 => "png",
                4 => "pdf",
                5 => "sha256",
                _ => "flac"
            };
            var fileName = count == 1
                ? $"{baseName}.{ext}"
                : $"{baseName} — {i + 1:D2}.{ext}";
            var baseLength = total / count;
            var length = i == count - 1 ? total - baseLength * (count - 1) : baseLength;
            files.Add(new TorrentFileSnapshot
            {
                RelativePath = fileName,
                FullPath = System.IO.Path.Combine(summary.SavePath, fileName),
                Length = length,
                Progress = summary.IsComplete ? 1 : Math.Clamp(summary.Progress + (i * 0.012), 0, 1),
                Priority = i == count - 1 && count > 3 ? FilePriority.DoNotDownload : FilePriority.Normal
            });
        }

        var trackers = new List<TrackerSnapshot>
        {
            new()
            {
                Url = "udp://tracker.opentrackr.org:1337/announce",
                Tier = 0,
                Status = "ok",
                StatusText = L.T("Working"),
                TimeSinceAnnounce = TimeSpan.FromSeconds(42),
                Succeeded = true
            },
            new()
            {
                Url = "https://tracker.gbitt.info:443/announce",
                Tier = 0,
                Status = "ok",
                StatusText = L.T("Working"),
                TimeSinceAnnounce = TimeSpan.FromMinutes(2),
                Succeeded = true
            },
            new()
            {
                Url = "udp://open.demonii.com:1337/announce",
                Tier = 1,
                Status = "offline",
                StatusText = L.T("Unavailable"),
                Message = L.T("Connection timeout"),
                TimeSinceAnnounce = TimeSpan.FromMinutes(12),
                Succeeded = false
            },
            new()
            {
                Url = "wss://tracker.webtorrent.dev",
                Tier = 1,
                Status = "unknown",
                StatusText = L.T("Waiting"),
                Succeeded = false
            }
        };

        var peers = new List<PeerSnapshot>
        {
            new() { Address = "185.125.190.58:51413", Client = "qBittorrent 5.1.2", Direction = L.T("Outgoing"), IsSeeder = true, Progress = 1, DownloadSpeed = 6_100_000, UploadSpeed = 810_000, Encryption = "RC4", Flags = "S" },
            new() { Address = "91.219.212.14:52841", Client = "Transmission 4.0.6", Direction = L.T("Incoming"), IsSeeder = true, Progress = 1, DownloadSpeed = 4_800_000, UploadSpeed = 1_200_000, Encryption = "RC4", Flags = "S" },
            new() { Address = "37.139.7.202:6881", Client = "Deluge 2.1.1", Direction = L.T("Outgoing"), IsSeeder = false, Progress = 0.84, DownloadSpeed = 3_100_000, UploadSpeed = 220_000, Encryption = L.T("Open") },
            new() { Address = "5.188.62.140:34122", Client = "qBittorrent 4.6.7", Direction = L.T("Incoming"), IsSeeder = false, Progress = 0.61, DownloadSpeed = 2_400_000, UploadSpeed = 0, Encryption = "RC4" },
            new() { Address = "203.0.113.42:51413", Client = "BiglyBT 3.7", Direction = L.T("Outgoing"), IsSeeder = false, Progress = 0.22, DownloadSpeed = 1_900_000, UploadSpeed = 80_000, Encryption = L.T("Open") },
            new() { Address = "[2001:470:1f0a:1c2::42]:51413", Client = "libtorrent 2.0.11", Direction = L.T("Incoming"), IsSeeder = false, Progress = 0.37, DownloadSpeed = 1_100_000, UploadSpeed = 0, Encryption = L.T("RC4 full") }
        };

        return new TorrentDetailsSnapshot
        {
            Summary = summary,
            Files = files,
            Trackers = trackers,
            Peers = peers,
            Comment = summary.IsPrivate ? "Private release" : "ZTorrent demo library — no network activity.",
            CreatedBy = "ZTorrent Demo",
            CreationDate = summary.AddedAt.AddDays(-2),
            SampledAt = DateTime.UtcNow
        };
    }

    private static string DemoDownloads(string name) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads", "ZTorrent", name);

    private static string DemoVideos(string name) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "ZTorrent", name);

    private static string DemoMusic(string name) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "ZTorrent", name);
}
