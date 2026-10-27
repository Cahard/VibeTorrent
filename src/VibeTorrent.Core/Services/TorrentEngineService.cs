using Microsoft.Extensions.Logging;
using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.Connections;
using MonoTorrent.Dht;
using MonoTorrent.Trackers;
using VibeTorrent.Core.Configuration;
using VibeTorrent.Core.Localization;
using VibeTorrent.Core.Models;
using VibeTorrent.Core.Utilities;

namespace VibeTorrent.Core.Services;

public interface ITorrentEngineService : IAsyncDisposable
{
    event EventHandler<EngineSnapshotEventArgs>? SnapshotUpdated;

    EngineSnapshot CurrentSnapshot { get; }

    AppSettings Settings { get; }

    bool IsInitialized { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<TorrentSnapshot> AddTorrentFileAsync(
        string torrentPath,
        AddTorrentOptions options,
        CancellationToken cancellationToken = default);

    Task<TorrentSnapshot> AddMagnetAsync(
        string magnet,
        AddTorrentOptions options,
        CancellationToken cancellationToken = default);

    Task PauseAsync(string id, CancellationToken cancellationToken = default);

    Task ResumeAsync(string id, CancellationToken cancellationToken = default);

    Task PauseAllAsync(CancellationToken cancellationToken = default);

    Task ResumeAllAsync(CancellationToken cancellationToken = default);

    Task RemoveAsync(string id, bool deleteData, CancellationToken cancellationToken = default);

    Task SetFilePriorityAsync(
        string id,
        string relativePath,
        FilePriority priority,
        CancellationToken cancellationToken = default);

    Task<TorrentDetailsSnapshot> GetDetailsAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task ApplySettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);

    Task SaveSessionAsync(CancellationToken cancellationToken = default);
}

public sealed class TorrentEngineService : ITorrentEngineService, IDisposable
{
    private readonly AppPaths _paths;
    private readonly ISettingsService _settingsService;
    private readonly ISessionStore _sessionStore;
    private readonly ITorrentPreviewService _previewService;
    private readonly ILogger<TorrentEngineService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, ManagedTorrent> _torrents = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FailedTorrent> _failed = new(StringComparer.OrdinalIgnoreCase);

    private AppSettings _settings;
    private ClientEngine? _engine;
    private CancellationTokenSource? _samplingCts;
    private Task? _samplingTask;
    private DateTime _startedAt;
    private int _listenPort;
    private bool _dhtEnabledAtStartup;
    private string? _startupWarning;
    private bool _initializing;
    private bool _initialized;
    private bool _disposed;
    private EngineSnapshot _currentSnapshot;

    public TorrentEngineService(
        AppPaths paths,
        ISettingsService settingsService,
        ISessionStore sessionStore,
        ITorrentPreviewService previewService,
        ILogger<TorrentEngineService> logger)
    {
        _paths = paths;
        _settingsService = settingsService;
        _sessionStore = sessionStore;
        _previewService = previewService;
        _logger = logger;
        _settings = settingsService.Current.Clone();
        _listenPort = _settings.ListenPort;
        _currentSnapshot = BuildEmptySnapshot();
    }

    public event EventHandler<EngineSnapshotEventArgs>? SnapshotUpdated;

    public EngineSnapshot CurrentSnapshot => _currentSnapshot;

    public AppSettings Settings => _settings.Clone();

    public bool IsInitialized => _initialized && !_disposed;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized || _initializing)
            {
                return;
            }

            _initializing = true;
            _paths.EnsureCreated();
            _settings = _settingsService.Current.Clone();
            _settings.Normalize();
            _listenPort = _settings.ListenPort;
            _engine = CreateEngine(_settings, ref _listenPort, ref _startupWarning);
            _dhtEnabledAtStartup = _settings.EnableDht;
            _startedAt = DateTime.UtcNow;
        }
        catch
        {
            _initializing = false;
            throw;
        }
        finally
        {
            _gate.Release();
        }

        try
        {
            // The snapshot loop must not run while restore mutates the manager
            // dictionaries. Start it only after the session is fully materialised.
            await RestoreSessionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Session restore failed.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _initialized = true;
            _initializing = false;
            _samplingCts = new CancellationTokenSource();
            _samplingTask = Task.Run(() => SamplingLoopAsync(_samplingCts.Token), CancellationToken.None);
            _logger.LogInformation("Torrent engine started. Port={Port}, DHT={Dht}, PEX={Pex}",
                _listenPort, _settings.EnableDht, _settings.EnablePex);
        }
        finally
        {
            _gate.Release();
        }

        await PublishSnapshotAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public async Task<TorrentSnapshot> AddTorrentFileAsync(
        string torrentPath,
        AddTorrentOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        EnsureInitialized();

        var preview = await _previewService.InspectTorrentFileAsync(torrentPath, cancellationToken)
            .ConfigureAwait(false);
        var savePath = PrepareSavePath(options.SavePath);
        var copyPath = Path.Combine(
            _paths.TorrentsDirectory,
            PathSafety.SafeTorrentFileName(preview.InfoHash) + ".torrent");

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var createdCopy = false;
        var engineAdded = false;
        TorrentManager? manager = null;
        string? addedId = null;
        try
        {
            if (_torrents.ContainsKey(preview.InfoHash) || _failed.ContainsKey(preview.InfoHash))
            {
                throw new TorrentValidationException(L.T("This torrent is already in the library."));
            }

            _paths.EnsureCreated();
            if (!Path.GetFullPath(torrentPath).Equals(Path.GetFullPath(copyPath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(torrentPath, copyPath, overwrite: true);
                createdCopy = true;
            }

            Torrent torrent;
            try
            {
                torrent = await Torrent.LoadAsync(copyPath).WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new TorrentValidationException(L.T("Could not prepare the torrent for download."), ex);
            }

            var infoHash = torrent.InfoHashes.V1OrV2?.ToHex()
                           ?? throw new TorrentValidationException(L.T("The torrent has no info hash."));
            if (!string.Equals(infoHash, preview.InfoHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new TorrentValidationException(L.T("The torrent info hash changed while reading."));
            }

            cancellationToken.ThrowIfCancellationRequested();

            // AddAsync has no cancellation support. Waiting with a token here can
            // abandon an already registered manager, so the engine call itself is
            // not cancellable; we only cancel before and after it.
            manager = await _engine!.AddAsync(
                    torrent,
                    savePath,
                    BuildTorrentSettings(_settings, torrent.IsPrivate))
                .ConfigureAwait(false);
            engineAdded = true;

            var record = new PersistedTorrent
            {
                Id = infoHash,
                Name = preview.Name,
                TorrentPath = copyPath,
                SavePath = savePath,
                StartPaused = !options.StartImmediately,
                AddedAt = DateTime.UtcNow,
                KnownSize = preview.TotalSize
            };

            var managed = new ManagedTorrent(infoHash, record, manager, preview.TotalSize)
            {
                MetadataSettingsApplied = true
            };
            addedId = infoHash;
            RegisterManager(managed);
            await ApplyPrioritiesAsync(managed, options.FilePriorities, cancellationToken).ConfigureAwait(false);
            managed.PrioritiesApplied = true;
            RecalculateSelectedBytes(managed);

            if (options.StartImmediately)
            {
                try
                {
                    await manager.StartAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The torrent is already part of the engine; keep it visible
                    // in a paused state instead of orphaning it and deleting its
                    // metadata.
                    record.StartPaused = true;
                    _logger.LogWarning(ex, "Torrent {Id} was added but could not start.", infoHash);
                }
            }

            UpdateRecordFromManager(managed);
            await PersistSessionLockedAsync(CancellationToken.None).ConfigureAwait(false);
            _logger.LogInformation("Torrent added: {Name} ({Id}) to {Path}", record.Name, infoHash, savePath);
        }
        catch
        {
            if (engineAdded && manager is not null)
            {
                try
                {
                    await manager.StopAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Best effort.
                }

                try
                {
                    await _engine!.RemoveAsync(manager, RemoveMode.CacheDataOnly).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not roll back a failed add.");
                }

                if (addedId is not null)
                {
                    _torrents.Remove(addedId);
                }
            }

            if (createdCopy)
            {
                TryDeleteFile(copyPath);
            }

            throw;
        }
        finally
        {
            _gate.Release();
        }

        await PublishSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return GetSnapshotOrThrow(preview.InfoHash);
    }

    public async Task<TorrentSnapshot> AddMagnetAsync(
        string magnet,
        AddTorrentOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        EnsureInitialized();

        var preview = _previewService.InspectMagnet(magnet);
        var savePath = PrepareSavePath(options.SavePath);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var engineAdded = false;
        TorrentManager? manager = null;
        try
        {
            if (_torrents.ContainsKey(preview.InfoHash) || _failed.ContainsKey(preview.InfoHash))
            {
                throw new TorrentValidationException(L.T("This torrent is already in the library."));
            }

            var link = MagnetLink.Parse(preview.Magnet!);
            cancellationToken.ThrowIfCancellationRequested();
            manager = await _engine!.AddAsync(
                    link,
                    savePath,
                    BuildTorrentSettings(_settings, isPrivate: false))
                .ConfigureAwait(false);
            engineAdded = true;

            var record = new PersistedTorrent
            {
                Id = preview.InfoHash,
                Name = preview.Name,
                Magnet = preview.Magnet,
                SavePath = savePath,
                StartPaused = !options.StartImmediately,
                AddedAt = DateTime.UtcNow,
                KnownSize = preview.TotalSize
            };

            var managed = new ManagedTorrent(preview.InfoHash, record, manager, preview.TotalSize)
            {
                MetadataSettingsApplied = false,
                PrioritiesApplied = options.FilePriorities is null or { Count: 0 }
            };
            RegisterManager(managed);
            await ApplyPrioritiesAsync(managed, options.FilePriorities, cancellationToken).ConfigureAwait(false);
            RecalculateSelectedBytes(managed);

            if (options.StartImmediately)
            {
                try
                {
                    await manager.StartAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    record.StartPaused = true;
                    _logger.LogWarning(ex, "Magnet {Id} was added but could not start.", preview.InfoHash);
                }
            }

            await PersistSessionLockedAsync(CancellationToken.None).ConfigureAwait(false);
            _logger.LogInformation("Magnet added: {Name} ({Id})", record.Name, record.Id);
        }
        catch
        {
            if (engineAdded && manager is not null)
            {
                try
                {
                    await manager.StopAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Best effort.
                }

                try
                {
                    await _engine!.RemoveAsync(manager, RemoveMode.CacheDataOnly).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not roll back a failed magnet add.");
                }

                _torrents.Remove(preview.InfoHash);
            }

            throw;
        }
        finally
        {
            _gate.Release();
        }

        await PublishSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return GetSnapshotOrThrow(preview.InfoHash);
    }

    public async Task PauseAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var managed = GetManagedOrThrow(id);
            if (managed.Manager.State is not (TorrentState.Paused or TorrentState.Stopped or TorrentState.Stopping or TorrentState.Error))
            {
                await managed.Manager.PauseAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            managed.Record.StartPaused = true;
            await PersistSessionLockedAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        await PublishSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ResumeAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var managed = GetManagedOrThrow(id);
            if (managed.Manager.State is not (TorrentState.Downloading or TorrentState.Seeding or TorrentState.Metadata or TorrentState.Starting))
            {
                await managed.Manager.StartAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            managed.Record.StartPaused = false;
            await PersistSessionLockedAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        await PublishSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task PauseAllAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var managed in _torrents.Values)
            {
                try
                {
                    if (managed.Manager.State is not (TorrentState.Paused or TorrentState.Stopped or TorrentState.Stopping or TorrentState.Error))
                    {
                        await managed.Manager.PauseAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
                    }

                    managed.Record.StartPaused = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not pause {Id}.", managed.Id);
                }
            }

            await PersistSessionLockedAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        await PublishSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ResumeAllAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var managed in _torrents.Values)
            {
                try
                {
                    if (managed.Manager.State is TorrentState.Paused or TorrentState.Stopped)
                    {
                        await managed.Manager.StartAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
                        managed.Record.StartPaused = false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not resume {Id}.", managed.Id);
                }
            }

            await PersistSessionLockedAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        await PublishSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAsync(string id, bool deleteData, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        string? torrentCopy = null;
        var filesToDelete = new List<string>();
        var savePath = string.Empty;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_failed.TryGetValue(id, out var failed))
            {
                torrentCopy = failed.Record.TorrentPath;
                _failed.Remove(id);
                await PersistSessionLockedAsync(CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                var managed = GetManagedOrThrow(id);
                savePath = managed.Record.SavePath;
                torrentCopy = managed.Record.TorrentPath;

                if (deleteData)
                {
                    foreach (var file in managed.Manager.Files)
                    {
                        AddCandidate(filesToDelete, file.FullPath);
                        AddCandidate(filesToDelete, file.DownloadCompleteFullPath);
                        AddCandidate(filesToDelete, file.DownloadIncompleteFullPath);
                    }
                }

                await managed.Manager.StopAsync(TimeSpan.FromSeconds(6))
                    .WaitAsync(cancellationToken).ConfigureAwait(false);

                _torrents.Remove(id);
                var mode = deleteData ? RemoveMode.CacheDataOnly : RemoveMode.KeepAllData;
                await _engine!.RemoveAsync(managed.Manager, mode)
                    .WaitAsync(cancellationToken).ConfigureAwait(false);

                if (deleteData && filesToDelete.Count > 0)
                {
                    var deleted = await SafeDataDeleter.DeleteFilesAsync(savePath, filesToDelete, CancellationToken.None)
                        .ConfigureAwait(false);
                    _logger.LogInformation("Removed {Count} data files for {Id}.", deleted, id);
                }

                await PersistSessionLockedAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }

        if (!string.IsNullOrWhiteSpace(torrentCopy) &&
            PathSafety.IsUnderRoot(_paths.TorrentsDirectory, torrentCopy))
        {
            TryDeleteFile(torrentCopy);
        }

        _logger.LogInformation("Torrent removed: {Id}. Data deleted: {DeleteData}", id, deleteData);
        await PublishSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetFilePriorityAsync(
        string id,
        string relativePath,
        FilePriority priority,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var managed = GetManagedOrThrow(id);
            if (!managed.Manager.HasMetadata || managed.Manager.Torrent is null)
            {
                throw new TorrentValidationException(L.T("Files are not available yet; wait for torrent metadata."));
            }

            var canonical = PathSafety.CanonicalRelativePath(relativePath);
            var index = FindFileIndex(managed.Manager.Torrent, canonical);
            if (index < 0 || index >= managed.Manager.Files.Count)
            {
                throw new ArgumentException(L.T("File not found in the torrent."), nameof(relativePath));
            }

            if (priority == FilePriority.DoNotDownload &&
                managed.Manager.Files.Count(file => file.Priority != Priority.DoNotDownload) <= 1 &&
                managed.Manager.Files[index].Priority != Priority.DoNotDownload)
            {
                throw new TorrentValidationException(L.T("You cannot skip every file; keep at least one."));
            }

            await managed.Manager
                .SetFilePriorityAsync(managed.Manager.Files[index], MapPriority(priority))
                .WaitAsync(cancellationToken).ConfigureAwait(false);

            managed.Record.FilePriorities[canonical] = priority;
            managed.PrioritiesApplied = true;
            RecalculateSelectedBytes(managed);
            await PersistSessionLockedAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        await PublishSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TorrentDetailsSnapshot> GetDetailsAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_failed.TryGetValue(id, out var failed))
            {
                var errorSnapshot = BuildFailedSnapshot(failed, DateTime.UtcNow);
                return new TorrentDetailsSnapshot
                {
                    Summary = errorSnapshot,
                    SampledAt = DateTime.UtcNow
                };
            }

            var managed = GetManagedOrThrow(id);
            var now = DateTime.UtcNow;
            var summary = BuildSnapshot(managed, now);
            var files = BuildFiles(managed);
            var trackers = BuildTrackers(managed);
            var peers = await BuildPeersAsync(managed, cancellationToken).ConfigureAwait(false);

            var comment = managed.Manager.HasMetadata ? managed.Manager.Torrent!.Comment : null;
            var createdBy = managed.Manager.HasMetadata ? managed.Manager.Torrent!.CreatedBy : null;
            DateTime? creation = null;
            if (managed.Manager.HasMetadata)
            {
                try
                {
                    creation = managed.Manager.Torrent!.CreationDate;
                }
                catch (Exception)
                {
                    creation = null;
                }
            }

            return new TorrentDetailsSnapshot
            {
                Summary = summary,
                Files = files,
                Trackers = trackers,
                Peers = peers,
                Comment = string.IsNullOrWhiteSpace(comment) ? null : comment,
                CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? null : createdBy,
                CreationDate = creation,
                SampledAt = now
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ApplySettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        EnsureInitialized();

        var errors = settings.Validate(_paths);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var oldPort = _settings.ListenPort;
            var oldDht = _settings.EnableDht;
            _settings = settings.Clone();

            // Reuse the currently bound port and DHT listener. MonoTorrent restarts
            // its listeners inside UpdateSettingsAsync; changing the endpoint here
            // could leave the engine deaf if the new port is busy. Port/DHT changes
            // are therefore applied on the next launch.
            var engineSettings = BuildEngineSettings(
                _settings, _paths, _listenPort, _dhtEnabledAtStartup, out _);
            var warnings = new List<string>();
            try
            {
                await _engine!.UpdateSettingsAsync(engineSettings).WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Engine-wide settings update failed; applying per-torrent settings.");
                warnings.Add(L.T("Some network settings will apply after restarting the app."));
            }

            foreach (var managed in _torrents.Values)
            {
                try
                {
                    var isPrivate = managed.Manager.HasMetadata && managed.Manager.Torrent!.IsPrivate;
                    await managed.Manager
                        .UpdateSettingsAsync(BuildTorrentSettings(_settings, isPrivate))
                        .WaitAsync(cancellationToken).ConfigureAwait(false);
                    managed.MetadataSettingsApplied = managed.Manager.HasMetadata;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not apply settings to torrent {Id}.", managed.Id);
                }
            }

            if (oldPort != _settings.ListenPort)
            {
                warnings.Add(L.T("The new listening port will apply after restarting the app."));
            }

            if (oldDht != _settings.EnableDht)
            {
                warnings.Add(L.T("The DHT change will apply after restarting the app."));
            }

            _startupWarning = warnings.Count == 0 ? null : string.Join(" ", warnings);
            await PersistSessionLockedAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        await PublishSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await PersistSessionLockedAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        // The DI container disposes singletons synchronously. The view model has
        // already performed the graceful async shutdown by this point, so this
        // is normally a no-op; the blocking fallback is only for direct users of
        // the service.
        if (_disposed)
        {
            return;
        }

        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _samplingCts?.Cancel();

        if (_samplingTask is not null)
        {
            try
            {
                await _samplingTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Sampling loop stopped with an error.");
            }
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_engine is not null)
            {
                try
                {
                    await _engine.StopAllAsync(TimeSpan.FromSeconds(8))
                        .WaitAsync(TimeSpan.FromSeconds(12))
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Engine did not stop gracefully.");
                }

                await PersistSessionLockedAsync(CancellationToken.None).ConfigureAwait(false);
                _engine.Dispose();
                _engine = null;
            }

            _torrents.Clear();
            _failed.Clear();
            _initialized = false;
            _initializing = false;
        }
        finally
        {
            _gate.Release();
            _samplingCts?.Dispose();
            _samplingCts = null;
        }

        _logger.LogInformation("Torrent engine disposed.");
    }

    private async Task RestoreSessionAsync(CancellationToken cancellationToken)
    {
        SessionState state;
        try
        {
            state = await _sessionStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load session.");
            return;
        }

        if (state.Torrents.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Restoring {Count} torrents...", state.Torrents.Count);
        var restored = 0;
        foreach (var record in state.Torrents.OrderBy(r => r.AddedAt))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await RestoreOneAsync(record, cancellationToken).ConfigureAwait(false);
                restored++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Could not restore torrent {Id}.", record.Id);
                await CleanupFailedRestoreAsync(record).ConfigureAwait(false);
                _failed[record.Id] = new FailedTorrent(
                    record,
                    L.F("Could not restore the torrent: {0}", ex.Message));
            }
        }

        _logger.LogInformation("Restored {Restored} of {Total} torrents.", restored, state.Torrents.Count);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await PersistSessionLockedAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task CleanupFailedRestoreAsync(PersistedTorrent record)
    {
        var partial = _torrents.Values
            .Where(t => ReferenceEquals(t.Record, record) ||
                        t.Id.Equals(record.Id, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var managed in partial)
        {
            _torrents.Remove(managed.Id);
            try
            {
                await managed.Manager.StopAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The manager may already be stopped after the failure.
            }

            try
            {
                await _engine!.RemoveAsync(managed.Manager, RemoveMode.CacheDataOnly).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not remove partially restored manager {Id}.", managed.Id);
            }
        }
    }

    private async Task RestoreOneAsync(PersistedTorrent record, CancellationToken cancellationToken)
    {
        if (!Path.IsPathFullyQualified(record.SavePath) ||
            record.SavePath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            throw new TorrentValidationException(L.T("The session contains an invalid download path."));
        }

        string? actualHash;
        TorrentManager manager;

        if (!string.IsNullOrWhiteSpace(record.TorrentPath) && File.Exists(record.TorrentPath))
        {
            var torrent = await Torrent.LoadAsync(record.TorrentPath).WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            actualHash = torrent.InfoHashes.V1OrV2?.ToHex();
            if (string.IsNullOrWhiteSpace(actualHash))
            {
                throw new TorrentValidationException(L.T("The saved torrent has no info hash."));
            }

            if (_torrents.ContainsKey(actualHash) || _failed.ContainsKey(actualHash))
            {
                throw new TorrentValidationException(L.T("Duplicate torrent in the session."));
            }

            if (!Directory.Exists(record.SavePath))
            {
                Directory.CreateDirectory(record.SavePath);
            }

            manager = await _engine!.AddAsync(
                    torrent,
                    record.SavePath,
                    BuildTorrentSettings(_settings, torrent.IsPrivate))
                .WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        else if (!string.IsNullOrWhiteSpace(record.Magnet))
        {
            var link = MagnetLink.Parse(record.Magnet);
            actualHash = link.InfoHashes.V1OrV2?.ToHex();
            if (string.IsNullOrWhiteSpace(actualHash))
            {
                throw new TorrentValidationException(L.T("The saved magnet link has no info hash."));
            }

            if (_torrents.ContainsKey(actualHash) || _failed.ContainsKey(actualHash))
            {
                throw new TorrentValidationException(L.T("Duplicate torrent in the session."));
            }

            if (!Directory.Exists(record.SavePath))
            {
                Directory.CreateDirectory(record.SavePath);
            }

            manager = await _engine!.AddAsync(
                    link,
                    record.SavePath,
                    BuildTorrentSettings(_settings, isPrivate: false))
                .WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            throw new TorrentValidationException(L.T("Both the .torrent file and the magnet link are missing."));
        }

        record.Id = actualHash;
        var managed = new ManagedTorrent(actualHash, record, manager, record.KnownSize)
        {
            // A magnet is initially added as public. When metadata reveals a
            // private flag, the pending-metadata pass reapplies DHT/PEX settings.
            MetadataSettingsApplied = string.IsNullOrWhiteSpace(record.Magnet) && manager.HasMetadata
        };
        RegisterManager(managed);
        await ApplyPrioritiesAsync(managed, record.FilePriorities, CancellationToken.None).ConfigureAwait(false);
        managed.PrioritiesApplied = manager.HasMetadata || record.FilePriorities.Count == 0;
        RecalculateSelectedBytes(managed);
        UpdateRecordFromManager(managed);

        var shouldStart = !record.StartPaused && _settings.AutoResumeOnStartup;
        if (shouldStart)
        {
            await manager.StartAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void RegisterManager(ManagedTorrent managed)
    {
        managed.Manager.TorrentStateChanged += (_, args) =>
        {
            if (args.NewState == TorrentState.Error)
            {
                _logger.LogWarning("Torrent {Id} entered error state: {Error}",
                    managed.Id, managed.Manager.Error?.Exception?.Message);
            }
        };
        _torrents[managed.Id] = managed;
    }

    private async Task ApplyPrioritiesAsync(
        ManagedTorrent managed,
        IReadOnlyDictionary<string, FilePriority>? priorities,
        CancellationToken cancellationToken)
    {
        if (priorities is null || priorities.Count == 0 || !managed.Manager.HasMetadata)
        {
            return;
        }

        foreach (var pair in priorities)
        {
            var canonical = PathSafety.CanonicalRelativePath(pair.Key);
            var index = FindFileIndex(managed.Manager.Torrent!, canonical);
            if (index < 0 || index >= managed.Manager.Files.Count)
            {
                continue;
            }

            await managed.Manager
                .SetFilePriorityAsync(managed.Manager.Files[index], MapPriority(pair.Value))
                .WaitAsync(cancellationToken).ConfigureAwait(false);
            managed.Record.FilePriorities[canonical] = pair.Value;
        }
    }

    /// <summary>
    /// Applies settings and file priorities that could not be evaluated before
    /// magnet metadata arrived. Called while the mutation gate is held.
    /// </summary>
    private async Task<bool> ApplyPendingMetadataLockedAsync(CancellationToken cancellationToken)
    {
        var changed = false;
        foreach (var managed in _torrents.Values.ToArray())
        {
            if (!managed.Manager.HasMetadata || managed.Manager.Torrent is null)
            {
                continue;
            }

            try
            {
                if (!managed.MetadataSettingsApplied)
                {
                    await managed.Manager
                        .UpdateSettingsAsync(BuildTorrentSettings(_settings, managed.Manager.Torrent.IsPrivate))
                        .WaitAsync(cancellationToken).ConfigureAwait(false);
                    managed.MetadataSettingsApplied = true;
                    changed = true;
                }

                if (managed.TotalBytes <= 0)
                {
                    RecalculateSelectedBytes(managed);
                    changed = true;
                }

                if (!managed.PrioritiesApplied && managed.Record.FilePriorities.Count > 0)
                {
                    await ApplyPrioritiesAsync(managed, managed.Record.FilePriorities, cancellationToken)
                        .ConfigureAwait(false);
                    RecalculateSelectedBytes(managed);
                    managed.PrioritiesApplied = true;
                    changed = true;
                }

                UpdateRecordFromManager(managed);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not apply pending metadata settings for {Id}.", managed.Id);
            }
        }

        return changed;
    }

    private void RecalculateSelectedBytes(ManagedTorrent managed)
    {
        if (!managed.Manager.HasMetadata)
        {
            managed.SelectedBytes = managed.Record.KnownSize;
            return;
        }

        long selected = 0;
        long total = 0;
        for (var i = 0; i < managed.Manager.Torrent!.Files.Count; i++)
        {
            var priority = i < managed.Manager.Files.Count
                ? managed.Manager.Files[i].Priority
                : Priority.Normal;
            var length = managed.Manager.Torrent!.Files[i].Length;
            total = total > long.MaxValue - length ? long.MaxValue : total + length;
            if (priority != Priority.DoNotDownload)
            {
                selected = selected > long.MaxValue - length ? long.MaxValue : selected + length;
            }
        }

        managed.TotalBytes = total;
        // Zero selected files is a valid (if unusual) state. Falling back to the
        // full size here would show 100% and send the engine on an endless hunt
        // for pieces it was told not to download.
        managed.SelectedBytes = selected;
    }

    private void UpdateRecordFromManager(ManagedTorrent managed)
    {
        if (!managed.Manager.HasMetadata)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(managed.Manager.Name))
        {
            managed.Record.Name = PathSafety.SanitizeDisplayName(managed.Manager.Name, managed.Record.Name);
        }

        managed.Record.KnownSize = managed.TotalBytes > 0
            ? managed.TotalBytes
            : managed.Manager.Torrent!.Size;
        managed.Record.LastKnownProgress = managed.Manager.Progress;
    }

    private List<TorrentFileSnapshot> BuildFiles(ManagedTorrent managed)
    {
        if (!managed.Manager.HasMetadata)
        {
            return [];
        }

        var result = new List<TorrentFileSnapshot>(managed.Manager.Torrent!.Files.Count);
        for (var i = 0; i < managed.Manager.Torrent!.Files.Count; i++)
        {
            var torrentFile = managed.Manager.Torrent!.Files[i];
            var managerFile = i < managed.Manager.Files.Count ? managed.Manager.Files[i] : null;
            var progress = managerFile?.BitField?.PercentComplete ?? 0;
            var priority = managerFile is null ? FilePriority.Normal : MapPriority(managerFile.Priority);
            result.Add(new TorrentFileSnapshot
            {
                RelativePath = PathSafety.CanonicalRelativePath(torrentFile.Path),
                FullPath = managerFile?.FullPath ?? string.Empty,
                Length = torrentFile.Length,
                Progress = Math.Clamp(progress, 0, 1),
                Priority = priority
            });
        }

        return result;
    }

    private static List<TrackerSnapshot> BuildTrackers(ManagedTorrent managed)
    {
        if (!managed.Manager.HasMetadata || managed.Manager.TrackerManager is null)
        {
            return [];
        }

        var result = new List<TrackerSnapshot>();
        var tiers = managed.Manager.TrackerManager.Tiers;
        for (var tierIndex = 0; tierIndex < tiers.Count; tierIndex++)
        {
            var tier = tiers[tierIndex];
            foreach (var tracker in tier.Trackers)
            {
                var message = tracker.FailureMessage ?? tracker.WarningMessage;
                result.Add(new TrackerSnapshot
                {
                    Url = tracker.Uri.ToString(),
                    Tier = tierIndex,
                    Status = tracker.Status.ToString().ToLowerInvariant(),
                    StatusText = MapTrackerState(tracker.Status),
                    Message = string.IsNullOrWhiteSpace(message) ? null : message,
                    TimeSinceAnnounce = tracker.TimeSinceLastAnnounce == TimeSpan.Zero
                        ? null
                        : tracker.TimeSinceLastAnnounce,
                    Succeeded = tier.LastAnnounceSucceeded
                });
            }
        }

        return result;
    }

    private static async Task<List<PeerSnapshot>> BuildPeersAsync(
        ManagedTorrent managed,
        CancellationToken cancellationToken)
    {
        try
        {
            var peers = await managed.Manager.GetPeersAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            return peers
                .OrderByDescending(p => p.Monitor?.DownloadRate ?? 0)
                .Take(250)
                .Select(peer => new PeerSnapshot
                {
                    Address = FormatPeerAddress(peer.Uri),
                    Client = peer.ClientApp.ToString(),
                    Direction = peer.ConnectionDirection switch
                    {
                        Direction.Incoming => L.T("Incoming"),
                        Direction.Outgoing => L.T("Outgoing"),
                        _ => "—"
                    },
                    IsSeeder = peer.IsSeeder,
                    Progress = Math.Clamp(peer.BitField?.PercentComplete ?? 0, 0, 1),
                    DownloadSpeed = peer.Monitor?.DownloadRate ?? 0,
                    UploadSpeed = peer.Monitor?.UploadRate ?? 0,
                    Encryption = peer.EncryptionType switch
                    {
                        EncryptionType.PlainText => L.T("Open"),
                        EncryptionType.RC4Header => "RC4",
                        EncryptionType.RC4Full => L.T("RC4 full"),
                        _ => "—"
                    },
                    Flags = peer.IsSeeder ? "S" : string.Empty
                })
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private TorrentSnapshot BuildSnapshot(ManagedTorrent managed, DateTime now)
    {
        var manager = managed.Manager;
        // Progress counts every file, while MonoTorrent may enter Seeding once the
        // selected subset is complete. PartialProgress keeps the UI honest when
        // some files are marked DoNotDownload.
        var progress = Math.Clamp(manager.PartialProgress, 0, 1);
        var total = manager.HasMetadata && managed.TotalBytes > 0
            ? managed.TotalBytes
            : managed.Record.KnownSize;
        var selected = Math.Max(0, managed.SelectedBytes);
        var downloaded = (long)Math.Min(selected, selected * progress);
        var upload = manager.Monitor.DataBytesSent;
        var ratio = manager.Monitor.DataBytesReceived > 0
            ? (double)upload / manager.Monitor.DataBytesReceived
            : 0;
        var speed = Math.Max(0, manager.Monitor.DownloadRate);
        var isComplete = progress >= 0.9999;
        long? eta = null;
        if (!isComplete && speed > 0 && selected > downloaded)
        {
            var seconds = (selected - downloaded) / (double)speed;
            if (seconds < TimeSpan.FromDays(365).TotalSeconds)
            {
                eta = (long)Math.Max(1, seconds);
            }
        }

        var state = MapState(manager, managed.Record.StartPaused, isComplete);
        var error = manager.Error?.Exception?.Message;

        return new TorrentSnapshot
        {
            Id = managed.Id,
            Name = PathSafety.SanitizeDisplayName(
                manager.HasMetadata ? manager.Name : managed.Record.Name),
            InfoHash = managed.Id,
            SavePath = managed.Record.SavePath,
            Status = state,
            Progress = progress,
            TotalSize = Math.Max(0, total),
            SelectedSize = Math.Max(managed.SelectedBytes, 0),
            DownloadedBytes = Math.Max(0, downloaded),
            UploadedBytes = Math.Max(0, upload),
            DownloadSpeed = speed,
            UploadSpeed = Math.Max(0, manager.Monitor.UploadRate),
            EtaSeconds = eta,
            PeerCount = Math.Max(manager.OpenConnections, manager.Peers.Available),
            SeedCount = manager.Peers.Seeds,
            LeechCount = manager.Peers.Leechs,
            FileCount = manager.HasMetadata ? manager.Torrent!.Files.Count : 0,
            Ratio = Math.Clamp(ratio, 0, 9999),
            IsPrivate = manager.HasMetadata && manager.Torrent!.IsPrivate,
            HasMetadata = manager.HasMetadata,
            IsPaused = managed.Record.StartPaused || manager.State is TorrentState.Paused,
            IsComplete = isComplete,
            AddedAt = managed.Record.AddedAt,
            ErrorMessage = string.IsNullOrWhiteSpace(error) ? null : error,
            SampledAt = now
        };
    }

    private TorrentSnapshot BuildFailedSnapshot(FailedTorrent failed, DateTime now) => new()
    {
        Id = failed.Record.Id,
        Name = PathSafety.SanitizeDisplayName(failed.Record.Name),
        InfoHash = failed.Record.Id,
        SavePath = failed.Record.SavePath,
        Status = TorrentStatus.Error,
        TotalSize = failed.Record.KnownSize,
        SelectedSize = failed.Record.KnownSize,
        AddedAt = failed.Record.AddedAt,
        HasMetadata = false,
        IsPaused = true,
        ErrorMessage = failed.Error,
        SampledAt = now
    };

    private EngineSnapshot BuildSnapshotLocked()
    {
        var now = DateTime.UtcNow;
        var snapshots = new List<TorrentSnapshot>(_torrents.Count + _failed.Count);
        foreach (var managed in _torrents.Values)
        {
            try
            {
                if (managed.Manager.HasMetadata && managed.Record.Name != managed.Manager.Name)
                {
                    managed.Record.Name = PathSafety.SanitizeDisplayName(managed.Manager.Name, managed.Record.Name);
                    managed.Record.KnownSize = managed.Manager.Torrent!.Size;
                }

                snapshots.Add(BuildSnapshot(managed, now));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not snapshot torrent {Id}.", managed.Id);
                snapshots.Add(new TorrentSnapshot
                {
                    Id = managed.Id,
                    Name = managed.Record.Name,
                    InfoHash = managed.Id,
                    SavePath = managed.Record.SavePath,
                    Status = TorrentStatus.Error,
                    AddedAt = managed.Record.AddedAt,
                    ErrorMessage = ex.Message,
                    SampledAt = now
                });
            }
        }

        foreach (var failed in _failed.Values)
        {
            snapshots.Add(BuildFailedSnapshot(failed, now));
        }

        snapshots.Sort(static (a, b) => b.AddedAt.CompareTo(a.AddedAt));

        var totalDownload = _torrents.Values.Sum(t => Math.Max(0, t.Manager.Monitor.DownloadRate));
        var totalUpload = _torrents.Values.Sum(t => Math.Max(0, t.Manager.Monitor.UploadRate));
        var totalDownloaded = _torrents.Values.Sum(t => Math.Max(0, t.Manager.Monitor.DataBytesReceived));
        var totalUploaded = _torrents.Values.Sum(t => Math.Max(0, t.Manager.Monitor.DataBytesSent));
        var dhtState = DhtState.NotReady;
        var dhtNodes = 0;
        try
        {
            if (_engine?.Dht is { } dht)
            {
                dhtState = dht.State;
                dhtNodes = dht.NodeCount;
            }
        }
        catch (Exception)
        {
            // DHT diagnostics are best-effort and must never break snapshots.
        }

        return new EngineSnapshot
        {
            Torrents = snapshots,
            SampledAt = now,
            Stats = new SessionStatsSnapshot
            {
                DownloadSpeed = totalDownload,
                UploadSpeed = totalUpload,
                DownloadedBytes = totalDownloaded,
                UploadedBytes = totalUploaded,
                TotalTorrents = snapshots.Count,
                ActiveTorrents = snapshots.Count(s => s.Status is TorrentStatus.Downloading or TorrentStatus.Seeding
                    or TorrentStatus.Metadata or TorrentStatus.Checking),
                SeedingTorrents = snapshots.Count(s => s.Status == TorrentStatus.Seeding),
                CompletedTorrents = snapshots.Count(s => s.IsComplete),
                PausedTorrents = snapshots.Count(s => s.Status == TorrentStatus.Paused),
                ErrorTorrents = snapshots.Count(s => s.Status == TorrentStatus.Error),
                // ClientEngine.IsRunning stays false until a managed torrent is
                // started, but the listener/DHT are already available after
                // construction. The engine is "online" for the UI once it exists.
                IsEngineRunning = _initialized && _engine is not null && !_disposed,
                DhtEnabled = _settings.EnableDht,
                DhtState = MapDhtState(dhtState),
                DhtNodes = dhtNodes,
                ListenPort = _listenPort,
                Uptime = _startedAt == default ? TimeSpan.Zero : now - _startedAt,
                Warning = _startupWarning,
                SampledAt = now
            }
        };
    }

    private EngineSnapshot BuildEmptySnapshot() => new()
    {
        Torrents = [],
        SampledAt = DateTime.UtcNow,
        Stats = new SessionStatsSnapshot
        {
            DhtEnabled = _settings.EnableDht,
            ListenPort = _settings.ListenPort,
            SampledAt = DateTime.UtcNow
        }
    };

    private async Task SamplingLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                {
                    return;
                }

                await PublishSnapshotAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // A single snapshot failure must not kill the UI forever.
                _logger.LogError(ex, "Snapshot tick failed; retrying.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private async Task PublishSnapshotAsync(CancellationToken cancellationToken)
    {
        if (_disposed || !_initialized)
        {
            return;
        }

        EngineSnapshot snapshot;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var changed = await ApplyPendingMetadataLockedAsync(cancellationToken).ConfigureAwait(false);
            snapshot = BuildSnapshotLocked();
            if (changed)
            {
                await PersistSessionLockedAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }

        _currentSnapshot = snapshot;
        try
        {
            SnapshotUpdated?.Invoke(this, new EngineSnapshotEventArgs(snapshot));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "A snapshot subscriber threw.");
        }
    }

    private ClientEngine CreateEngine(AppSettings settings, ref int port, ref string? warning)
    {
        try
        {
            var engineSettings = BuildEngineSettings(settings, _paths, port, settings.EnableDht, out _);
            return new ClientEngine(engineSettings);
        }
        catch (Exception ex) when (port != 0)
        {
            _logger.LogWarning(ex, "Could not listen on port {Port}; falling back to a random port.", port);
            port = 0;
            warning = L.F("Port {0} is busy; using a random port.", settings.ListenPort);
            var fallback = BuildEngineSettings(settings, _paths, 0, settings.EnableDht, out _);
            return new ClientEngine(fallback);
        }
    }

    private string PrepareSavePath(string? requested)
    {
        var path = string.IsNullOrWhiteSpace(requested)
            ? _settings.DownloadDirectory
            : requested;

        if (!Path.IsPathFullyQualified(path))
        {
            throw new TorrentValidationException(L.T("The download folder must be an absolute path."));
        }

        if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            throw new TorrentValidationException(L.T("Network UNC folders are not supported. Choose a local drive."));
        }

        try
        {
            path = Path.GetFullPath(path);
            Directory.CreateDirectory(path);
            var probe = Path.Combine(path, $".vibetorrent-write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
        }
        catch (Exception ex)
        {
            throw new TorrentValidationException(L.T("The download folder is not writable."), ex);
        }

        return path;
    }

    private static EngineSettings BuildEngineSettings(
        AppSettings settings,
        AppPaths paths,
        int port,
        bool enableDht,
        out int effectivePort)
    {
        var builder = new EngineSettingsBuilder
        {
            CacheDirectory = paths.CacheDirectory,
            AllowPortForwarding = settings.EnableUpnp,
            AllowLocalPeerDiscovery = settings.EnableLsd,
            AutoSaveLoadDhtCache = true,
            AutoSaveLoadFastResume = true,
            AutoSaveLoadMagnetLinkMetadata = true,
            FastResumeMode = FastResumeMode.Accurate,
            DiskCacheBytes = settings.DiskCacheMb * 1024 * 1024,
            MaximumConnections = settings.MaxConnections,
            MaximumHalfOpenConnections = Math.Clamp(settings.MaxConnections / 3, 8, 100),
            MaximumDownloadRate = ToBytesPerSecond(settings.MaxDownloadKbps),
            MaximumUploadRate = ToBytesPerSecond(settings.MaxUploadKbps),
            MaximumOpenFiles = 256,
            UsePartialFiles = false,
            DiskCachePolicy = MonoTorrent.PieceWriter.CachePolicy.WritesOnly,
            // In MonoTorrent 3.0.2 the presence of DhtEndPoint is the switch that
            // creates the DHT node. A null endpoint is the only way to disable it.
            DhtEndPoint = enableDht
                ? new System.Net.IPEndPoint(System.Net.IPAddress.Any, port)
                : null
        };

        builder.ListenEndPoints.Clear();
        builder.ListenEndPoints["ipv4"] = new System.Net.IPEndPoint(System.Net.IPAddress.Any, port);
        if (System.Net.Sockets.Socket.OSSupportsIPv6)
        {
            builder.ListenEndPoints["ipv6"] = new System.Net.IPEndPoint(System.Net.IPAddress.IPv6Any, port);
        }

        builder.ReportedListenEndPoints.Clear();
        builder.AllowedEncryption.Clear();
        if (settings.EnableEncryption)
        {
            builder.AllowedEncryption.Add(EncryptionType.RC4Header);
            builder.AllowedEncryption.Add(EncryptionType.RC4Full);
        }

        builder.AllowedEncryption.Add(EncryptionType.PlainText);
        effectivePort = port;
        // MonoTorrent derives FastResumeCacheDirectory as
        // <CacheDirectory>/fastresume. AppPaths.FastResumeDirectory points to
        // exactly that directory and EnsureCreated() creates it before start.
        return builder.ToSettings();
    }

    private static TorrentSettings BuildTorrentSettings(AppSettings settings, bool isPrivate)
    {
        return new TorrentSettingsBuilder
        {
            AllowDht = settings.EnableDht && !isPrivate,
            AllowPeerExchange = settings.EnablePex && !isPrivate,
            AllowInitialSeeding = true,
            CreateContainingDirectory = true,
            MaximumConnections = settings.MaxConnections,
            MaximumDownloadRate = ToBytesPerSecond(settings.MaxDownloadKbps),
            MaximumUploadRate = ToBytesPerSecond(settings.MaxUploadKbps),
            UploadSlots = settings.MaxUploadSlots,
            RequirePeerIdToMatch = false
        }.ToSettings();
    }

    private static int ToBytesPerSecond(int kilobytesPerSecond) =>
        (int)Math.Min(int.MaxValue, Math.Max(0, kilobytesPerSecond) * 1024L);

    private static TorrentStatus MapState(TorrentManager manager, bool userPaused, bool complete) =>
        manager.State switch
        {
            TorrentState.Metadata => TorrentStatus.Metadata,
            TorrentState.Hashing or TorrentState.HashingPaused or TorrentState.FetchingHashes => TorrentStatus.Checking,
            TorrentState.Downloading => TorrentStatus.Downloading,
            TorrentState.Seeding => TorrentStatus.Seeding,
            TorrentState.Starting => TorrentStatus.Queued,
            TorrentState.Stopping => userPaused ? TorrentStatus.Paused : TorrentStatus.Stopped,
            TorrentState.Paused => TorrentStatus.Paused,
            TorrentState.Stopped => userPaused ? TorrentStatus.Paused : complete ? TorrentStatus.Completed : TorrentStatus.Stopped,
            TorrentState.Error => TorrentStatus.Error,
            _ => TorrentStatus.Stopped
        };

    private static string MapDhtState(DhtState state) => state switch
    {
        DhtState.Ready => L.T("Connected"),
        DhtState.Initialising => L.T("Initializing"),
        DhtState.NotReady => L.T("Waiting"),
        _ => L.T("Waiting")
    };

    private static string MapTrackerState(TrackerState state) => state switch
    {
        TrackerState.Ok => L.T("Working"),
        TrackerState.Connecting => L.T("Connecting"),
        TrackerState.Offline => L.T("Unavailable"),
        TrackerState.InvalidResponse => L.T("Invalid response"),
        _ => L.T("Waiting")
    };

    private static Priority MapPriority(FilePriority priority) => priority switch
    {
        FilePriority.DoNotDownload => Priority.DoNotDownload,
        FilePriority.Low => Priority.Low,
        FilePriority.High => Priority.High,
        FilePriority.Maximum => Priority.Highest,
        _ => Priority.Normal
    };

    private static FilePriority MapPriority(Priority priority) => priority switch
    {
        Priority.DoNotDownload => FilePriority.DoNotDownload,
        Priority.Lowest or Priority.Low => FilePriority.Low,
        Priority.High => FilePriority.High,
        Priority.Highest or Priority.Immediate => FilePriority.Maximum,
        _ => FilePriority.Normal
    };

    private static int FindFileIndex(Torrent? torrent, string canonicalPath)
    {
        if (torrent is null)
        {
            return -1;
        }

        for (var i = 0; i < torrent.Files.Count; i++)
        {
            if (string.Equals(
                    PathSafety.CanonicalRelativePath(torrent.Files[i].Path),
                    canonicalPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static string FormatPeerAddress(Uri uri)
    {
        var text = uri.ToString();
        var index = text.IndexOf("://", StringComparison.Ordinal);
        return index >= 0 ? text[(index + 3)..].TrimEnd('/') : text;
    }

    private async Task PersistSessionLockedAsync(CancellationToken cancellationToken)
    {
        var state = new SessionState
        {
            Torrents = _torrents.Values.Select(t => CloneRecord(t.Record))
                .Concat(_failed.Values.Select(f => CloneRecord(f.Record)))
                .OrderBy(r => r.AddedAt)
                .ToList()
        };

        try
        {
            // Session persistence is a critical mutation; it must not be dropped
            // just because the command that triggered it was cancelled.
            await _sessionStore.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save session.");
        }
    }

    private static PersistedTorrent CloneRecord(PersistedTorrent record) => new()
    {
        Id = record.Id,
        Name = record.Name,
        TorrentPath = record.TorrentPath,
        Magnet = record.Magnet,
        SavePath = record.SavePath,
        StartPaused = record.StartPaused,
        AddedAt = record.AddedAt,
        KnownSize = record.KnownSize,
        LastKnownProgress = record.LastKnownProgress,
        FilePriorities = new Dictionary<string, FilePriority>(
            record.FilePriorities, StringComparer.OrdinalIgnoreCase)
    };

    private TorrentSnapshot GetSnapshotOrThrow(string id)
    {
        return CurrentSnapshot.Torrents.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException(L.T("Torrent not found in the state snapshot."));
    }

    private ManagedTorrent GetManagedOrThrow(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !_torrents.TryGetValue(id, out var managed))
        {
            throw new KeyNotFoundException(L.T("Torrent not found."));
        }

        return managed;
    }

    private void EnsureInitialized()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized)
        {
            throw new InvalidOperationException(L.T("The torrent engine is not initialized yet."));
        }
    }

    private static void AddCandidate(ICollection<string> target, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            target.Add(path);
        }
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // A stale copy is harmless; it is ignored on the next start.
        }
    }

    private sealed class ManagedTorrent
    {
        public ManagedTorrent(string id, PersistedTorrent record, TorrentManager manager, long selectedBytes)
        {
            Id = id;
            Record = record;
            Manager = manager;
            SelectedBytes = selectedBytes;
        }

        public string Id { get; }
        public PersistedTorrent Record { get; }
        public TorrentManager Manager { get; }
        public long SelectedBytes { get; set; }
        public long TotalBytes { get; set; }
        public bool MetadataSettingsApplied { get; set; }
        public bool PrioritiesApplied { get; set; }
    }

    private sealed class FailedTorrent
    {
        public FailedTorrent(PersistedTorrent record, string error)
        {
            Record = record;
            Error = error;
        }

        public PersistedTorrent Record { get; }
        public string Error { get; }
    }
}

