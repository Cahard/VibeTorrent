using System.Text.Json;
using Microsoft.Extensions.Logging;
using VibeTorrent.Core.Configuration;
using VibeTorrent.Core.Models;
using VibeTorrent.Core.Utilities;

namespace VibeTorrent.Core.Services;

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
            SessionState? state;
            try
            {
                state = await JsonSupport.ReadAsync<SessionState>(_paths.SessionFile, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                var backup = JsonSupport.QuarantineCorruptFile(_paths.SessionFile);
                _logger.LogWarning(ex, "Session file was corrupt. Backup: {Backup}", backup);
                return new SessionState();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not read session file.");
                return new SessionState();
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

    public async Task SaveAsync(SessionState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _paths.EnsureCreated();
            await JsonSupport.WriteAtomicAsync(_paths.SessionFile, state, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
