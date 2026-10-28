using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using ZTorrent.Core.Services;

namespace ZTorrent.Tests;

public sealed class SettingsAndSessionTests
{
    [Fact]
    public async Task Settings_RoundTrip_AndInvalidValuesAreRejected()
    {
        using var workspace = new TempWorkspace();
        var service = new SettingsService(workspace.Paths, NullLogger<SettingsService>.Instance);
        var settings = workspace.CreateSettings();
        settings.MaxDownloadKbps = 4096;
        settings.EnableDht = true;

        await service.SaveAsync(settings);
        Assert.True(File.Exists(workspace.Paths.SettingsFile));

        var reloaded = await new SettingsService(workspace.Paths, NullLogger<SettingsService>.Instance).LoadAsync();
        Assert.Equal(4096, reloaded.MaxDownloadKbps);
        Assert.True(reloaded.EnableDht);

        var invalid = settings.Clone();
        invalid.ListenPort = 12;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(invalid));
    }

    [Fact]
    public void Settings_Validation_RejectsUncDownloadDirectory()
    {
        using var workspace = new TempWorkspace();
        var settings = workspace.CreateSettings();
        settings.DownloadDirectory = @"\\server\share\incoming";

        var errors = settings.Validate(workspace.Paths);

        Assert.Contains(errors, error => error.Contains("UNC", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Settings_CorruptFile_IsQuarantined_AndDefaultsLoad()
    {
        using var workspace = new TempWorkspace();
        await File.WriteAllTextAsync(workspace.Paths.SettingsFile, "{ definitely not json", Encoding.UTF8);

        var service = new SettingsService(workspace.Paths, NullLogger<SettingsService>.Instance);
        var settings = await service.LoadAsync();

        Assert.NotEqual(12, settings.ListenPort);
        Assert.NotEmpty(settings.DownloadDirectory);
        Assert.NotEmpty(Directory.GetFiles(workspace.Paths.RootDirectory, "settings.json.corrupt-*"));
    }

    [Fact]
    public async Task Session_NullTorrents_DoesNotCrash()
    {
        using var workspace = new TempWorkspace();
        await File.WriteAllTextAsync(workspace.Paths.SessionFile, """{"version":1,"torrents":null}""");
        var store = new SessionStore(workspace.Paths, NullLogger<SessionStore>.Instance);

        var state = await store.LoadAsync();
        Assert.Empty(state.Torrents);
    }

    [Fact]
    public async Task Session_SaveLoad_DeduplicatesAndNormalizesPriorities()
    {
        using var workspace = new TempWorkspace();
        var store = new SessionStore(workspace.Paths, NullLogger<SessionStore>.Instance);
        var state = new Core.Models.SessionState
        {
            Torrents =
            [
                new Core.Models.PersistedTorrent
                {
                    Id = "abc",
                    SavePath = workspace.Download,
                    TorrentPath = Path.Combine(workspace.Paths.TorrentsDirectory, "abc.torrent"),
                    AddedAt = DateTime.UtcNow,
                    FilePriorities = new Dictionary<string, Core.Models.FilePriority>
                    {
                        ["file.mkv"] = Core.Models.FilePriority.DoNotDownload
                    }
                },
                new Core.Models.PersistedTorrent
                {
                    Id = "abc",
                    SavePath = workspace.Download,
                    TorrentPath = Path.Combine(workspace.Paths.TorrentsDirectory, "abc.torrent"),
                    AddedAt = DateTime.UtcNow
                }
            ]
        };

        await store.SaveAsync(state);
        var loaded = await store.LoadAsync();
        var record = Assert.Single(loaded.Torrents);
        Assert.Equal(Core.Models.FilePriority.DoNotDownload, record.FilePriorities["file.mkv"]);
    }
}
