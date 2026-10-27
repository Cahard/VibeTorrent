using System.Text.Json;
using Microsoft.Extensions.Logging;
using VibeTorrent.Core.Configuration;
using VibeTorrent.Core.Utilities;

namespace VibeTorrent.Core.Services;

public interface ISettingsService
{
    AppSettings Current { get; }

    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public sealed class SettingsService : ISettingsService
{
    private readonly AppPaths _paths;
    private readonly ILogger<SettingsService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AppSettings _current;

    public SettingsService(AppPaths paths, ILogger<SettingsService> logger)
    {
        _paths = paths;
        _logger = logger;
        _current = AppSettings.Default;
        _current.DownloadDirectory = paths.DownloadsDirectory;
    }

    public AppSettings Current => _current;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AppSettings settings;
            try
            {
                settings = await JsonSupport.ReadAsync<AppSettings>(_paths.SettingsFile, cancellationToken)
                               .ConfigureAwait(false) ?? AppSettings.Default;
            }
            catch (JsonException ex)
            {
                var backup = JsonSupport.QuarantineCorruptFile(_paths.SettingsFile);
                _logger.LogWarning(ex, "Settings file was corrupt. Backup: {Backup}", backup);
                settings = AppSettings.Default;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not read settings. Falling back to defaults.");
                settings = AppSettings.Default;
            }

            if (string.IsNullOrWhiteSpace(settings.DownloadDirectory))
            {
                settings.DownloadDirectory = _paths.DownloadsDirectory;
            }

            if (settings.Normalize())
            {
                _logger.LogInformation("Settings were normalized to safe defaults.");
            }

            _current = settings;
            return settings;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var errors = settings.Validate(_paths);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _paths.EnsureCreated();
            await JsonSupport.WriteAtomicAsync(_paths.SettingsFile, settings, cancellationToken)
                .ConfigureAwait(false);
            _current = settings.Clone();
        }
        finally
        {
            _gate.Release();
        }
    }
}
