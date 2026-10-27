using MonoTorrent;

namespace VibeTorrent.Tests;

internal sealed class SyntheticTorrent : IDisposable
{
    public required string Root { get; init; }
    public required string TorrentPath { get; init; }
    public required string PayloadDirectory { get; init; }
    public required string TorrentName { get; init; }
    public required IReadOnlyList<string> FileNames { get; init; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (Exception)
        {
            // Best effort.
        }
    }
}

internal static class SyntheticTorrentFactory
{
    public static async Task<SyntheticTorrent> CreateAsync(string? root = null, string? announce = null)
    {
        var workingRoot = root ?? Path.Combine(Path.GetTempPath(), "VibeTorrent.Torrents", Guid.NewGuid().ToString("N"));
        var payload = Path.Combine(workingRoot, "payload");
        Directory.CreateDirectory(payload);
        File.WriteAllBytes(Path.Combine(payload, "alpha.bin"), Enumerable.Range(0, 32_768).Select(i => (byte)(i % 251)).ToArray());
        File.WriteAllBytes(Path.Combine(payload, "beta.bin"), Enumerable.Range(0, 65_536).Select(i => (byte)(i % 239)).ToArray());

        var torrentPath = Path.Combine(workingRoot, "fixture.torrent");
        var creator = new TorrentCreator
        {
            PieceLength = 16 * 1024,
            CreatedBy = "VibeTorrent.Tests",
            Comment = "Synthetic local fixture",
            Announce = announce
        };

        var source = new TorrentFileSource(payload) { TorrentName = "Synthetic Release" };
        await creator.CreateAsync(source, torrentPath);

        return new SyntheticTorrent
        {
            Root = workingRoot,
            TorrentPath = torrentPath,
            PayloadDirectory = payload,
            TorrentName = "Synthetic Release",
            FileNames = ["alpha.bin", "beta.bin"]
        };
    }
}
