using Microsoft.Extensions.Logging.Abstractions;
using ZTorrent.Core.Configuration;
using ZTorrent.Core.Services;

namespace ZTorrent.Tests;

internal sealed class TempWorkspace : IDisposable
{
    public TempWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "ZTorrent.Tests", Guid.NewGuid().ToString("N"));
        Download = Path.Combine(Root, "downloads");
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Download);
        Paths = AppPaths.Create(Path.Combine(Root, "state"), Download);
        Paths.EnsureCreated();
    }

    public string Root { get; }
    public string Download { get; }
    public AppPaths Paths { get; }

    public AppSettings CreateSettings(int port = 0) => new()
    {
        DownloadDirectory = Download,
        ListenPort = port == 0 ? TestPorts.GetFreeTcpPort() : port,
        EnableDht = false,
        EnablePex = false,
        EnableLsd = false,
        EnableUpnp = false,
        MaxConnections = 30,
        MaxUploadSlots = 2,
        DiskCacheMb = 8,
        AutoResumeOnStartup = false,
        CloseToTray = false,
        ShowNotifications = false
    };

    public SettingsService CreateSettingsService()
    {
        var service = new SettingsService(Paths, NullLogger<SettingsService>.Instance);
        return service;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (Exception)
        {
            // Test cleanup is best effort; a locked file from a failed engine
            // should not hide the actual test result.
        }
    }
}

internal static class TestPorts
{
    public static int GetFreeTcpPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }
}
