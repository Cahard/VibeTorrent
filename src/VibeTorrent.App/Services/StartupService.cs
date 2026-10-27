using Microsoft.Win32;

namespace VibeTorrent.App.Services;

public interface IStartupService
{
    bool TrySetEnabled(bool enabled, out string? error);
}

public sealed class StartupService : IStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "VibeTorrent";

    public bool TrySetEnabled(bool enabled, out string? error)
    {
        error = null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                error = "Не удалось открыть раздел автозапуска.";
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
                error = "Не удалось определить путь к приложению.";
                return false;
            }

            key.SetValue(ValueName, $"\"{processPath}\" --minimized", RegistryValueKind.String);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Автозапуск не изменён: {ex.Message}";
            return false;
        }
    }
}
