using System.Diagnostics;
using VibeTorrent.Core.Localization;

namespace VibeTorrent.App.Services;

public interface IPowerService
{
    bool TryExecute(string action, out string? error);
}

/// <summary>
/// Executes the user's post-completion action. Sleep is S3
/// (SetSuspendState without hibernate); hibernate uses shutdown.exe /h so an
/// unavailable hibernation file is reported instead of silently sleeping.
/// </summary>
public sealed class PowerService : IPowerService
{
    public bool TryExecute(string? action, out string? error)
    {
        error = null;
        switch (action?.Trim().ToLowerInvariant())
        {
            case null or "" or "none":
                return true;

            case "shutdown":
                return Start("shutdown.exe", "/s /t 0", wait: false, out error);

            case "restart":
                return Start("shutdown.exe", "/r /t 0", wait: false, out error);

            case "hibernate":
                return Start("shutdown.exe", "/h", wait: true, out error);

            case "sleep":
                return Start("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0", wait: true, out error);

            default:
                error = L.T("Unknown power action.");
                return false;
        }
    }

    private static bool Start(string fileName, string arguments, bool wait, out string? error)
    {
        error = null;
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            if (process is null)
            {
                error = L.T("The power action could not be started.");
                return false;
            }

            if (!wait)
            {
                return true;
            }

            if (!process.WaitForExit(5000))
            {
                error = L.T("The power action did not finish.");
                return false;
            }

            if (process.ExitCode != 0)
            {
                error = L.T("Windows rejected the power action. Hibernation may be disabled in Windows settings.");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
