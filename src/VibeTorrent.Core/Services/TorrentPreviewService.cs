using Microsoft.Extensions.Logging;
using MonoTorrent;
using VibeTorrent.Core.Models;
using VibeTorrent.Core.Utilities;

namespace VibeTorrent.Core.Services;

public interface ITorrentPreviewService
{
    Task<TorrentPreview> InspectTorrentFileAsync(string path, CancellationToken cancellationToken = default);

    TorrentPreview InspectMagnet(string magnet);
}

public sealed class TorrentPreviewService : ITorrentPreviewService
{
    private const long MaxTorrentFileBytes = 32L * 1024 * 1024;
    private const int MaxFiles = 100_000;
    private const int MaxMagnetLength = 32 * 1024;

    private readonly ILogger<TorrentPreviewService> _logger;

    public TorrentPreviewService(ILogger<TorrentPreviewService> logger)
    {
        _logger = logger;
    }

    public async Task<TorrentPreview> InspectTorrentFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new TorrentValidationException("Не указан путь к .torrent файлу.");
        }

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new TorrentValidationException("Файл .torrent не найден.");
        }

        var info = new FileInfo(fullPath);
        if (info.Length <= 0)
        {
            throw new TorrentValidationException("Файл .torrent пуст.");
        }

        if (info.Length > MaxTorrentFileBytes)
        {
            throw new TorrentValidationException("Файл .torrent слишком большой (более 32 МБ).");
        }

        Torrent torrent;
        try
        {
            torrent = await Torrent.LoadAsync(fullPath).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid torrent file.");
            throw new TorrentValidationException("Не удалось прочитать .torrent: файл повреждён или не является торрентом.", ex);
        }

        return BuildPreview(torrent, fullPath, null);
    }

    public TorrentPreview InspectMagnet(string magnet)
    {
        if (string.IsNullOrWhiteSpace(magnet))
        {
            throw new TorrentValidationException("Вставьте magnet-ссылку.");
        }

        var trimmed = magnet.Trim();
        if (trimmed.Length > MaxMagnetLength)
        {
            throw new TorrentValidationException("Magnet-ссылка слишком длинная.");
        }

        if (!trimmed.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
        {
            throw new TorrentValidationException("Ссылка должна начинаться с magnet:?");
        }

        if (!MagnetLink.TryParse(trimmed, out var link) || link.InfoHashes.V1OrV2 is null)
        {
            throw new TorrentValidationException("Некорректная magnet-ссылка: не найден info hash.");
        }

        var hash = link.InfoHashes.V1OrV2.ToHex();
        return new TorrentPreview
        {
            Name = PathSafety.SanitizeDisplayName(link.Name, "Загрузка по magnet-ссылке"),
            InfoHash = hash,
            TotalSize = link.Size ?? 0,
            HasMetadata = false,
            Magnet = trimmed,
            Trackers = link.AnnounceUrls
                .Select(static uri => uri?.ToString() ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    private static TorrentPreview BuildPreview(Torrent torrent, string sourcePath, string? magnet)
    {
        if (torrent.Files is null || torrent.Files.Count == 0)
        {
            throw new TorrentValidationException("В торренте нет файлов.");
        }

        if (torrent.Files.Count > MaxFiles)
        {
            throw new TorrentValidationException($"Слишком много файлов в торренте ({torrent.Files.Count:N0}).");
        }

        var files = new List<PreviewFile>(torrent.Files.Count);
        long total = 0;
        foreach (var file in torrent.Files)
        {
            string safe;
            try
            {
                safe = PathSafety.ValidateRelativePath(file.Path);
            }
            catch (ArgumentException ex)
            {
                throw new TorrentValidationException(
                    $"Торрент содержит небезопасный путь: {ex.Message}", ex);
            }

            total = total > long.MaxValue - file.Length ? long.MaxValue : total + file.Length;
            files.Add(new PreviewFile { Path = safe, Length = file.Length });
        }

        var hash = torrent.InfoHashes.V1OrV2?.ToHex()
                   ?? throw new TorrentValidationException("В торренте отсутствует info hash.");

        var trackers = torrent.AnnounceUrls
            .SelectMany(static tier => tier ?? Array.Empty<string>())
            .Select(static uri => uri?.Trim() ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new TorrentPreview
        {
            Name = PathSafety.SanitizeDisplayName(torrent.Name),
            InfoHash = hash,
            TotalSize = total,
            IsPrivate = torrent.IsPrivate,
            HasMetadata = true,
            Comment = string.IsNullOrWhiteSpace(torrent.Comment) ? null : torrent.Comment,
            CreatedBy = string.IsNullOrWhiteSpace(torrent.CreatedBy) ? null : torrent.CreatedBy,
            Files = files,
            Trackers = trackers,
            SourcePath = sourcePath,
            Magnet = magnet
        };
    }
}
