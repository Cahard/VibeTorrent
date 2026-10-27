using Microsoft.Extensions.Logging.Abstractions;
using VibeTorrent.Core.Models;
using VibeTorrent.Core.Services;

namespace VibeTorrent.Tests;

public sealed class TorrentEngineIntegrationTests
{
    [Fact]
    public async Task AddPriorityAndRemove_RespectSafetyAndDataChoice()
    {
        using var workspace = new TempWorkspace();
        using var fixture = await SyntheticTorrentFactory.CreateAsync();
        var settingsService = workspace.CreateSettingsService();
        var settings = workspace.CreateSettings();
        await settingsService.SaveAsync(settings);

        await using var engine = new TorrentEngineService(
            workspace.Paths,
            settingsService,
            new SessionStore(workspace.Paths, NullLogger<SessionStore>.Instance),
            new TorrentPreviewService(NullLogger<TorrentPreviewService>.Instance),
            NullLogger<TorrentEngineService>.Instance);

        await engine.InitializeAsync();
        var snapshot = await engine.AddTorrentFileAsync(
            fixture.TorrentPath,
            new AddTorrentOptions
            {
                SavePath = workspace.Download,
                StartImmediately = false
            });

        Assert.Equal(2, snapshot.FileCount);
        Assert.True(snapshot.IsPaused);
        Assert.True(snapshot.TotalSize > 0);
        Assert.Equal(snapshot.TotalSize, snapshot.SelectedSize);

        await engine.ResumeAsync(snapshot.Id);
        Assert.False(engine.CurrentSnapshot.Torrents.Single(t => t.Id == snapshot.Id).IsPaused);
        await engine.PauseAsync(snapshot.Id);
        Assert.True(engine.CurrentSnapshot.Torrents.Single(t => t.Id == snapshot.Id).IsPaused);

        var details = await engine.GetDetailsAsync(snapshot.Id);
        Assert.Equal(2, details.Files.Count);
        Assert.All(details.Files, file => Assert.Equal(FilePriority.Normal, file.Priority));

        await engine.SetFilePriorityAsync(snapshot.Id, details.Files[0].RelativePath, FilePriority.DoNotDownload);
        var afterPriority = await engine.GetDetailsAsync(snapshot.Id);
        Assert.Equal(FilePriority.DoNotDownload, afterPriority.Files[0].Priority);
        Assert.True(afterPriority.Summary.SelectedSize < afterPriority.Summary.TotalSize);

        await Assert.ThrowsAsync<TorrentValidationException>(() =>
            engine.SetFilePriorityAsync(snapshot.Id, afterPriority.Files[1].RelativePath, FilePriority.DoNotDownload));

        // Simulate payload that would be deleted with the torrent.
        var dataDirectory = Path.Combine(workspace.Download, fixture.TorrentName);
        Directory.CreateDirectory(dataDirectory);
        var dataFile = Path.Combine(dataDirectory, fixture.FileNames[0]);
        File.WriteAllText(dataFile, "local data");

        await engine.RemoveAsync(snapshot.Id, deleteData: false);
        Assert.True(File.Exists(dataFile));
        Assert.Empty(engine.CurrentSnapshot.Torrents);

        var again = await engine.AddTorrentFileAsync(
            fixture.TorrentPath,
            new AddTorrentOptions { SavePath = workspace.Download, StartImmediately = false });
        Assert.True(File.Exists(dataFile));

        await engine.RemoveAsync(again.Id, deleteData: true);
        Assert.False(File.Exists(dataFile));
        Assert.Empty(engine.CurrentSnapshot.Torrents);
    }

    [Fact]
    public async Task SessionRestore_RecreatesPausedTorrent()
    {
        using var workspace = new TempWorkspace();
        using var fixture = await SyntheticTorrentFactory.CreateAsync();
        var settingsService = workspace.CreateSettingsService();
        var settings = workspace.CreateSettings();
        await settingsService.SaveAsync(settings);

        string id;
        await using (var first = new TorrentEngineService(
                         workspace.Paths,
                         settingsService,
                         new SessionStore(workspace.Paths, NullLogger<SessionStore>.Instance),
                         new TorrentPreviewService(NullLogger<TorrentPreviewService>.Instance),
                         NullLogger<TorrentEngineService>.Instance))
        {
            await first.InitializeAsync();
            var snapshot = await first.AddTorrentFileAsync(
                fixture.TorrentPath,
                new AddTorrentOptions
                {
                    SavePath = workspace.Download,
                    StartImmediately = false,
                    FilePriorities = new Dictionary<string, FilePriority>
                    {
                        ["alpha.bin"] = FilePriority.DoNotDownload
                    }
                });
            id = snapshot.Id;
        }

        var reloadedSettings = await new SettingsService(workspace.Paths, NullLogger<SettingsService>.Instance)
            .LoadAsync();
        Assert.False(reloadedSettings.AutoResumeOnStartup);

        await using var second = new TorrentEngineService(
            workspace.Paths,
            new SettingsService(workspace.Paths, NullLogger<SettingsService>.Instance),
            new SessionStore(workspace.Paths, NullLogger<SessionStore>.Instance),
            new TorrentPreviewService(NullLogger<TorrentPreviewService>.Instance),
            NullLogger<TorrentEngineService>.Instance);
        await second.InitializeAsync();

        var restored = Assert.Single(second.CurrentSnapshot.Torrents);
        Assert.Equal(id, restored.Id);
        Assert.True(restored.IsPaused);
        Assert.Equal(2, restored.FileCount);
    }
}
