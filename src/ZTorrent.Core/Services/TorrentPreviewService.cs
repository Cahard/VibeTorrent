using Microsoft.Extensions.Logging;
using MonoTorrent;
using ZTorrent.Core.Localization;
using ZTorrent.Core.Models;
using ZTorrent.Core.Utilities;

namespace ZTorrent.Core.Services;

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
            throw new TorrentValidationException(L.T("No path to the .torrent file was provided."));
        }

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new TorrentValidationException(L.T("The .torrent file was not found."));
        }

        var info = new FileInfo(fullPath);
        if (info.Length <= 0)
        {
            throw new TorrentValidationException(L.T("The .torrent file is empty."));
        }

        if (info.Length > MaxTorrentFileBytes)
        {
            throw new TorrentValidationException(L.T("The .torrent file is too large (over 32 MB)."));
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
            throw new TorrentValidationException(L.T("Could not read the .torrent: the file is damaged or not a torrent."), ex);
        }

        return BuildPreview(torrent, fullPath, null);
    }

    public TorrentPreview InspectMagnet(string magnet)
    {
        if (string.IsNullOrWhiteSpace(magnet))
        {
            throw new TorrentValidationException(L.T("Paste a magnet link."));
        }

        var trimmed = magnet.Trim();
        if (trimmed.Length > MaxMagnetLength)
        {
            throw new TorrentValidationException(L.T("The magnet link is too long."));
        }

        if (!trimmed.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
        {
            throw new TorrentValidationException(L.T("The link must start with magnet:?"));
        }

        if (!MagnetLink.TryParse(trimmed, out var link) || link.InfoHashes.V1OrV2 is null)
        {
            throw new TorrentValidationException(L.T("Invalid magnet link: no info hash found."));
        }

        var hash = link.InfoHashes.V1OrV2.ToHex();
        return new TorrentPreview
        {
            Name = PathSafety.SanitizeDisplayName(link.Name, L.T("Magnet download")),
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
            throw new TorrentValidationException(L.T("The torrent contains no files."));
        }

        if (torrent.Files.Count > MaxFiles)
        {
            throw new TorrentValidationException(L.F("Too many files in the torrent ({0:N0}).", torrent.Files.Count));
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
                    L.F("The torrent contains an unsafe path: {0}", ex.Message), ex);
            }

            total = total > long.MaxValue - file.Length ? long.MaxValue : total + file.Length;
            files.Add(new PreviewFile { Path = safe, Length = file.Length });
        }

        var hash = torrent.InfoHashes.V1OrV2?.ToHex()
                   ?? throw new TorrentValidationException(L.T("The torrent has no info hash."));

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
