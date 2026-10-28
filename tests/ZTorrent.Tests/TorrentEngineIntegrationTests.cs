using Microsoft.Extensions.Logging.Abstractions;
using ZTorrent.Core.Models;
using ZTorrent.Core.Services;

namespace ZTorrent.Tests;

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
    public async Task MoveDownloadFolder_PreservesMultiFileLayoutAndMovesData()
    {
        using var workspace = new TempWorkspace();
        using var fixture = await SyntheticTorrentFactory.CreateAsync();
        var settingsService = workspace.CreateSettingsService();
        await settingsService.SaveAsync(workspace.CreateSettings());

        await using var engine = new TorrentEngineService(
            workspace.Paths,
            settingsService,
            new SessionStore(workspace.Paths, NullLogger<SessionStore>.Instance),
            new TorrentPreviewService(NullLogger<TorrentPreviewService>.Instance),
            NullLogger<TorrentEngineService>.Instance);

        await engine.InitializeAsync();
        var added = await engine.AddTorrentFileAsync(
            fixture.TorrentPath,
            new AddTorrentOptions
            {
                SavePath = workspace.Download,
                StartImmediately = false
            });

        var oldContaining = Path.Combine(workspace.Download, fixture.TorrentName);
        Directory.CreateDirectory(oldContaining);
        var oldFile = Path.Combine(oldContaining, fixture.FileNames[0]);
        await File.WriteAllTextAsync(oldFile, "payload that must move with the torrent");

        var movedRoot = Path.Combine(workspace.Root, "moved-downloads");
        var moved = await engine.MoveDownloadFolderAsync(added.Id, movedRoot);
        var expected = Path.Combine(movedRoot, fixture.TorrentName, fixture.FileNames[0]);

        Assert.Equal(movedRoot, moved.SavePath, ignoreCase: true);
        Assert.True(File.Exists(expected));
        Assert.False(File.Exists(oldFile));
        Assert.Equal("payload that must move with the torrent", await File.ReadAllTextAsync(expected));

        var details = await engine.GetDetailsAsync(added.Id);
        Assert.Contains(details.Files, file => file.FullPath.Equals(expected, StringComparison.OrdinalIgnoreCase));
        Assert.All(details.Files, file =>
            Assert.StartsWith(movedRoot, file.FullPath, StringComparison.OrdinalIgnoreCase));

        await engine.DisposeAsync();
        await using var restoredEngine = new TorrentEngineService(
            workspace.Paths,
            new SettingsService(workspace.Paths, NullLogger<SettingsService>.Instance),
            new SessionStore(workspace.Paths, NullLogger<SessionStore>.Instance),
            new TorrentPreviewService(NullLogger<TorrentPreviewService>.Instance),
            NullLogger<TorrentEngineService>.Instance);
        await restoredEngine.InitializeAsync();
        var restored = Assert.Single(restoredEngine.CurrentSnapshot.Torrents);
        Assert.Equal(movedRoot, restored.SavePath, ignoreCase: true);
        Assert.True(File.Exists(expected));
    }

    [Fact]
    public async Task PriorityAndLimits_PersistAcrossRestart()
    {
        using var workspace = new TempWorkspace();
        using var fixture = await SyntheticTorrentFactory.CreateAsync();
        var settingsService = workspace.CreateSettingsService();
        await settingsService.SaveAsync(workspace.CreateSettings());

        string id;
        await using (var first = new TorrentEngineService(
                         workspace.Paths,
                         settingsService,
                         new SessionStore(workspace.Paths, NullLogger<SessionStore>.Instance),
                         new TorrentPreviewService(NullLogger<TorrentPreviewService>.Instance),
                         NullLogger<TorrentEngineService>.Instance))
        {
            await first.InitializeAsync();
            var added = await first.AddTorrentFileAsync(
                fixture.TorrentPath,
                new AddTorrentOptions { SavePath = workspace.Download, StartImmediately = false });
            id = added.Id;

            await first.SetTorrentPriorityAsync(id, TorrentPriority.High);
            var limited = await first.SetTorrentLimitsAsync(id, 2048, 512);
            Assert.Equal(TorrentPriority.High, limited.Priority);
            Assert.Equal(2048, limited.DownloadLimitKbps);
            Assert.Equal(512, limited.UploadLimitKbps);
        }

        await using var second = new TorrentEngineService(
            workspace.Paths,
            new SettingsService(workspace.Paths, NullLogger<SettingsService>.Instance),
            new SessionStore(workspace.Paths, NullLogger<SessionStore>.Instance),
            new TorrentPreviewService(NullLogger<TorrentPreviewService>.Instance),
            NullLogger<TorrentEngineService>.Instance);
        await second.InitializeAsync();
        var restored = Assert.Single(second.CurrentSnapshot.Torrents);
        Assert.Equal(id, restored.Id);
        Assert.Equal(TorrentPriority.High, restored.Priority);
        Assert.Equal(2048, restored.DownloadLimitKbps);
        Assert.Equal(512, restored.UploadLimitKbps);
    }

    [Fact]
    public async Task MoveDownloadFolder_ConflictFailsWithoutTouchingSource()
    {
        using var workspace = new TempWorkspace();
        using var fixture = await SyntheticTorrentFactory.CreateAsync();
        var settingsService = workspace.CreateSettingsService();
        await settingsService.SaveAsync(workspace.CreateSettings());

        await using var engine = new TorrentEngineService(
            workspace.Paths,
            settingsService,
            new SessionStore(workspace.Paths, NullLogger<SessionStore>.Instance),
            new TorrentPreviewService(NullLogger<TorrentPreviewService>.Instance),
            NullLogger<TorrentEngineService>.Instance);

        await engine.InitializeAsync();
        var added = await engine.AddTorrentFileAsync(
            fixture.TorrentPath,
            new AddTorrentOptions { SavePath = workspace.Download, StartImmediately = false });

        var oldContaining = Path.Combine(workspace.Download, fixture.TorrentName);
        Directory.CreateDirectory(oldContaining);
        var oldFile = Path.Combine(oldContaining, fixture.FileNames[0]);
        await File.WriteAllTextAsync(oldFile, "source payload");

        var target = Path.Combine(workspace.Root, "conflict-target");
        var conflicting = Path.Combine(target, fixture.TorrentName, fixture.FileNames[0]);
        Directory.CreateDirectory(Path.GetDirectoryName(conflicting)!);
        await File.WriteAllTextAsync(conflicting, "other payload");

        await Assert.ThrowsAsync<TorrentValidationException>(() =>
            engine.MoveDownloadFolderAsync(added.Id, target));

        Assert.True(File.Exists(oldFile));
        Assert.Equal("source payload", await File.ReadAllTextAsync(oldFile));
        Assert.Equal(workspace.Download, engine.CurrentSnapshot.Torrents.Single(t => t.Id == added.Id).SavePath,
            ignoreCase: true);
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
