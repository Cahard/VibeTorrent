using System.Text;
using ZTorrent.Core.Localization;

namespace ZTorrent.Core.Utilities;

/// <summary>
/// Validation helpers for untrusted torrent metadata. Path traversal is the one
/// class of bug that can turn a torrent client into an arbitrary file writer, so
/// every file name that originates from a torrent passes through here.
/// </summary>
public static class PathSafety
{
    private static readonly char[] InvalidWindowsChars = ['<', '>', ':', '"', '|', '?', '*'];
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string CanonicalRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Replace('\\', '/').Trim();
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized.TrimStart('/');
    }

    /// <summary>
    /// Returns a canonical relative path or throws when the path could escape
    /// the download root. We deliberately do not rewrite path segments: the
    /// file layout must match the torrent's piece layout exactly.
    /// </summary>
    public static string ValidateRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(L.T("Empty path in torrent metadata."), nameof(path));
        }

        if (path.IndexOf('\0') >= 0)
        {
            throw new ArgumentException(L.T("The path contains a null character."), nameof(path));
        }

        if (path.StartsWith(@"\\", StringComparison.Ordinal) ||
            path.StartsWith("//", StringComparison.Ordinal))
        {
            throw new ArgumentException(L.T("UNC paths are not allowed in torrent metadata."), nameof(path));
        }

        var normalized = CanonicalRelativePath(path);
        var segments = normalized.Split('/', StringSplitOptions.None);
        if (segments.Length == 0)
        {
            throw new ArgumentException(L.T("Empty path in torrent metadata."), nameof(path));
        }

        foreach (var segment in segments)
        {
            if (segment.Length == 0 ||
                segment.Length > 240 ||
                segment is "." or ".." ||
                segment.EndsWith(' ') ||
                segment.EndsWith('.') ||
                segment.Any(ch => InvalidWindowsChars.Contains(ch)))
            {
                throw new ArgumentException(L.F("Unsafe path in torrent: \"{0}\".", path), nameof(path));
            }

            // A colon in a file segment is an NTFS alternate data stream and can
            // hide content from the user; it is never needed in torrent metadata.
            if (segment.Contains(':'))
            {
                throw new ArgumentException(L.F("ADS streams are not allowed in torrent paths: \"{0}\".", path), nameof(path));
            }

            var stem = segment.Split('.')[0];
            if (ReservedNames.Contains(stem))
            {
                throw new ArgumentException(L.F("Reserved file name in torrent: \"{0}\".", segment), nameof(path));
            }

            if (segment.Any(char.IsControl))
            {
                throw new ArgumentException(L.T("Control character in torrent path."), nameof(path));
            }
        }

        if (Path.IsPathRooted(path) ||
            normalized.StartsWith("//", StringComparison.Ordinal) ||
            (normalized.Length >= 2 && normalized[1] == ':'))
        {
            throw new ArgumentException(L.T("Absolute paths are not allowed in torrent metadata."), nameof(path));
        }

        return normalized;
    }

    public static bool IsUnderRoot(string root, string candidate)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        try
        {
            var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            var fullCandidate = Path.GetFullPath(candidate);
            if (string.Equals(fullRoot, fullCandidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var relative = Path.GetRelativePath(fullRoot, fullCandidate);
            return !relative.StartsWith("..", StringComparison.Ordinal) &&
                   !Path.IsPathRooted(relative);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string SanitizeDisplayName(string? name, string? fallback = null)
    {
        fallback ??= L.T("Untitled");
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }

        var builder = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            if (char.IsControl(ch) || ch == '\u202E')
            {
                continue;
            }

            builder.Append(ch);
        }

        var cleaned = string.Join(' ', builder.ToString().Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();

        if (cleaned.Length == 0)
        {
            return fallback;
        }

        return cleaned.Length <= 240 ? cleaned : cleaned[..239] + "…";
    }

    public static string SanitizeFileSegment(string segment, string fallback = "_")
    {
        var builder = new StringBuilder(segment.Length);
        foreach (var ch in segment)
        {
            builder.Append(InvalidWindowsChars.Contains(ch) || char.IsControl(ch) ? '_' : ch);
        }

        var result = builder.ToString().Trim().TrimEnd('.');
        if (result.Length == 0)
        {
            result = fallback;
        }

        var stem = result.Split('.')[0];
        return ReservedNames.Contains(stem) ? "_" + result : result;
    }

    public static string SafeTorrentFileName(string infoHash)
    {
        var safe = new string(infoHash.Where(char.IsAsciiLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? Guid.NewGuid().ToString("N") : safe.ToLowerInvariant();
    }
}
