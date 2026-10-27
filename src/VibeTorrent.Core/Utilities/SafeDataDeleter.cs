namespace VibeTorrent.Core.Utilities;

public static class SafeDataDeleter
{
    /// <summary>
    /// Deletes only files that belong to this torrent and are provably under the
    /// chosen save root. Never deletes the save root itself, even if a malformed
    /// torrent tries to point at it.
    /// </summary>
    public static async Task<int> DeleteFilesAsync(
        string saveRoot,
        IEnumerable<string> files,
        CancellationToken cancellationToken = default)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(saveRoot));
        var deleted = 0;
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(file))
            {
                continue;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(file);
            }
            catch (Exception)
            {
                continue;
            }

            if (!PathSafety.IsUnderRoot(root, fullPath) ||
                string.Equals(root, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                if (File.Exists(fullPath))
                {
                    var attributes = File.GetAttributes(fullPath);
                    if (attributes.HasFlag(FileAttributes.ReadOnly))
                    {
                        File.SetAttributes(fullPath, attributes & ~FileAttributes.ReadOnly);
                    }

                    File.Delete(fullPath);
                    deleted++;
                }

                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(directory) &&
                    !string.Equals(directory, root, StringComparison.OrdinalIgnoreCase) &&
                    PathSafety.IsUnderRoot(root, directory))
                {
                    directories.Add(directory);
                }
            }
            catch (Exception)
            {
                // A locked file or a permission error must not abort the removal of
                // the torrent itself. The caller reports the count and the UI keeps
                // the remaining files available for a manual retry.
            }
        }

        foreach (var directory in directories.OrderByDescending(d => d.Length))
        {
            TryDeleteEmptyDirectoryChain(directory, root);
        }

        await Task.CompletedTask.ConfigureAwait(false);
        return deleted;
    }

    private static void TryDeleteEmptyDirectoryChain(string directory, string root)
    {
        var current = directory;
        while (PathSafety.IsUnderRoot(root, current) &&
               !string.Equals(current, root, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (!Directory.Exists(current) ||
                    Directory.EnumerateFileSystemEntries(current).Any())
                {
                    return;
                }

                Directory.Delete(current);
                var parent = Path.GetDirectoryName(current);
                if (string.IsNullOrWhiteSpace(parent))
                {
                    return;
                }

                current = parent;
            }
            catch (Exception)
            {
                return;
            }
        }
    }
}
