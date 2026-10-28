using System.Diagnostics;
using Microsoft.Win32;
using ZTorrent.Core.Localization;

namespace ZTorrent.App.Services;

public interface IDialogService
{
    string? PickTorrentFile();

    string? PickFolder(string? initialDirectory);

    void OpenFolder(string path);

    void RevealInExplorer(string path);
}

public sealed class DialogService : IDialogService
{
    public string? PickTorrentFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = L.T("Select a torrent file"),
            Filter = L.T("Torrent files (*.torrent)|*.torrent|All files (*.*)|*.*"),
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder(string? initialDirectory)
    {
        var dialog = new OpenFolderDialog
        {
            Title = L.T("Download folder"),
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public void OpenFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var target = Directory.Exists(path)
                ? path
                : Path.GetDirectoryName(path);

            if (string.IsNullOrWhiteSpace(target) || !Directory.Exists(target))
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{target}\"",
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
            // Opening a folder is a convenience; never crash the client for it.
        }
    }

    public void RevealInExplorer(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });
                return;
            }

            OpenFolder(path);
        }
        catch (Exception)
        {
            // Ignore explorer failures.
        }
    }
}
