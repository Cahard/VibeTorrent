using Microsoft.Extensions.Logging.Abstractions;
using ZTorrent.Core.Configuration;
using ZTorrent.Core.Models;
using ZTorrent.Core.Services;

namespace ZTorrent.Tests;

public sealed class SessionStoreTests
{
    [Fact]
    public async Task Load_RestoresBackupWhenPrimarySessionIsMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), "ZTorrent.Session", Guid.NewGuid().ToString("N"));
        var downloads = Path.Combine(root, "downloads");
        var paths = AppPaths.Create(root, downloads);
        paths.EnsureCreated();
        try
        {
            var store = new SessionStore(paths, NullLogger<SessionStore>.Instance);
            await store.SaveAsync(new SessionState
            {
                Torrents =
                [
                    new PersistedTorrent
                    {
                        Id = "0123456789ABCDEF0123456789ABCDEF01234567",
                        Name = "Remembered",
                        TorrentPath = Path.Combine(paths.TorrentsDirectory, "remembered.torrent"),
                        SavePath = downloads,
                        AddedAt = DateTime.UtcNow
                    }
                ]
            });

            File.Delete(paths.SessionFile);
            var restored = await store.LoadAsync();

            var torrent = Assert.Single(restored.Torrents);
            Assert.Equal("Remembered", torrent.Name);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (Exception)
            {
                // Best effort.
            }
        }
    }
}
