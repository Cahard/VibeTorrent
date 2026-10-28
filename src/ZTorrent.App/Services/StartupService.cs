using Microsoft.Win32;
using ZTorrent.Core.Localization;

namespace ZTorrent.App.Services;

public interface IStartupService
{
    bool TrySetEnabled(bool enabled, out string? error);
}

public sealed class StartupService : IStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ZTorrent";

    public bool TrySetEnabled(bool enabled, out string? error)
    {
        error = null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                error = L.T("Could not open the startup registry key.");
                return false;
            }

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return true;
            }

            var processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath))
            {
                error = L.T("Could not determine the application path.");
                return false;
            }

            key.SetValue(ValueName, $"\"{processPath}\" --minimized", RegistryValueKind.String);
            return true;
        }
        catch (Exception ex)
        {
            error = L.F("Autostart was not changed: {0}", ex.Message);
            return false;
        }
    }
}
