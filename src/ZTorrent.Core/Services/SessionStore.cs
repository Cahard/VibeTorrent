using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZTorrent.Core.Configuration;
using ZTorrent.Core.Models;
using ZTorrent.Core.Utilities;

namespace ZTorrent.Core.Services;

public interface ISessionStore
{
    Task<SessionState> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(SessionState state, CancellationToken cancellationToken = default);
}

public sealed class SessionStore : ISessionStore
{
    private readonly AppPaths _paths;
    private readonly ILogger<SessionStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string BackupFile => _paths.SessionFile + ".bak";

    public SessionStore(AppPaths paths, ILogger<SessionStore> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public async Task<SessionState> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await ReadSessionFileAsync(_paths.SessionFile, quarantine: true, cancellationToken)
                .ConfigureAwait(false);
            if (state is null)
            {
                state = await ReadSessionFileAsync(BackupFile, quarantine: false, cancellationToken)
                    .ConfigureAwait(false);
                if (state is not null)
                {
                    _logger.LogWarning("Restored the torrent session from its backup file.");
                }
            }

            if (state is null)
            {
                return new SessionState();
            }

            state.Torrents ??= [];
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var valid = new List<PersistedTorrent>(state.Torrents.Count);
            foreach (var torrent in state.Torrents)
            {
                if (torrent is null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(torrent.Id) ||
                    string.IsNullOrWhiteSpace(torrent.SavePath) ||
                    !Path.IsPathFullyQualified(torrent.SavePath) ||
                    (string.IsNullOrWhiteSpace(torrent.TorrentPath) && string.IsNullOrWhiteSpace(torrent.Magnet)) ||
                    !seen.Add(torrent.Id))
                {
                    _logger.LogWarning("Skipping invalid session record {Id}.", torrent.Id);
                    continue;
                }

                torrent.FilePriorities = new Dictionary<string, FilePriority>(
                    torrent.FilePriorities ?? new Dictionary<string, FilePriority>(),
                    StringComparer.OrdinalIgnoreCase);
                if (!Enum.IsDefined(torrent.Priority))
                {
                    torrent.Priority = TorrentPriority.Normal;
                }

                torrent.DownloadLimitKbps = Math.Clamp(torrent.DownloadLimitKbps, 0, 10_000_000);
                torrent.UploadLimitKbps = Math.Clamp(torrent.UploadLimitKbps, 0, 10_000_000);
                valid.Add(torrent);
            }

            state.Torrents = valid;
            return state;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<SessionState?> ReadSessionFileAsync(
        string path,
        bool quarantine,
        CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSupport.ReadAsync<SessionState>(path, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            if (quarantine)
            {
                var backup = JsonSupport.QuarantineCorruptFile(path);
                _logger.LogWarning(ex, "Session file was corrupt. Backup: {Backup}", backup);
            }
            else
            {
                _logger.LogWarning(ex, "Session backup was corrupt.");
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not read session file {Path}.", path);
            return null;
        }
    }

    public async Task SaveAsync(SessionState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _paths.EnsureCreated();
            await JsonSupport.WriteAtomicAsync(_paths.SessionFile, state, cancellationToken)
                .ConfigureAwait(false);
            if (state.Torrents.Count > 0)
            {
                try
                {
                    File.Copy(_paths.SessionFile, BackupFile, overwrite: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogDebug(ex, "Could not refresh the session backup.");
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}
