using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using VibeTorrent.Core.Configuration;

namespace VibeTorrent.App.Services;

public sealed record UpdateInfo(
    Version Version,
    string Tag,
    string DownloadUrl,
    string? Sha256Url);

public interface IUpdateService
{
    Version CurrentVersion { get; }

    Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken = default);

    Task<string> DownloadAsync(UpdateInfo update, CancellationToken cancellationToken = default);

    bool LaunchInstaller(string installerPath);
}

/// <summary>
/// Talks to the public GitHub Releases API and stages the signed installer in
/// the app data directory. The actual install is delegated to a detached
/// PowerShell helper so the app can close before Inno Setup replaces it.
/// </summary>
public sealed class UpdateService : IUpdateService
{
    private const string ApiUrl = "https://api.github.com/repos/Cahard/VibeTorrent/releases/latest";
    private readonly AppPaths _paths;
    private readonly HttpClient _http;

    public UpdateService(AppPaths paths)
    {
        _paths = paths;
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("VibeTorrent-Updater/1.0");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public Version CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0, 0);

    public async Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(ApiUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var root = document.RootElement;
        var tag = root.TryGetProperty("tag_name", out var tagElement)
            ? tagElement.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(tag) ||
            !Version.TryParse(tag.TrimStart('v', 'V'), out var version) ||
            version <= CurrentVersion)
        {
            return null;
        }

        string? downloadUrl = null;
        string? shaUrl = null;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                var url = asset.TryGetProperty("browser_download_url", out var urlElement)
                    ? urlElement.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                if (name.Equals("VibeTorrent-Setup.exe", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = url;
                }
                else if (name.Equals("VibeTorrent-Setup.exe.sha256", StringComparison.OrdinalIgnoreCase))
                {
                    shaUrl = url;
                }
            }
        }

        return downloadUrl is null
            ? null
            : new UpdateInfo(version, tag, downloadUrl, shaUrl);
    }

    public async Task<string> DownloadAsync(UpdateInfo update, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_paths.UpdatesDirectory);
        var target = Path.Combine(
            _paths.UpdatesDirectory,
            $"VibeTorrent-Setup-{update.Version.ToString(3)}.exe");

        using (var response = await _http.GetAsync(update.DownloadUrl, cancellationToken).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var destination = File.Create(target);
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(update.Sha256Url))
        {
            var expected = (await _http.GetStringAsync(update.Sha256Url, cancellationToken)
                    .ConfigureAwait(false))
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(expected))
            {
                await using var stream = File.OpenRead(target);
                var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)
                    .ConfigureAwait(false));
                if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(target);
                    throw new InvalidOperationException("The update checksum does not match.");
                }
            }
        }

        return target;
    }

    public bool LaunchInstaller(string installerPath)
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath) || !File.Exists(installerPath))
        {
            return false;
        }

        var scriptPath = Path.Combine(Path.GetTempPath(), $"VibeTorrent-update-{Guid.NewGuid():N}.ps1");
        var installDir = Path.GetDirectoryName(exePath) ?? string.Empty;
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            Start-Sleep -Milliseconds 1200
            $installerArgs = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CLOSEAPPLICATIONS','/DIR={{Escape(installDir)}}')
            $process = Start-Process -FilePath '{{Escape(installerPath)}}' -ArgumentList $installerArgs -Wait -PassThru
            if (Test-Path '{{Escape(exePath)}}') {
                Start-Process -FilePath '{{Escape(exePath)}}'
            }
            Remove-Item -LiteralPath $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue
            """;
        File.WriteAllText(scriptPath, script);

        try
        {
            var start = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(start);
            return true;
        }
        catch
        {
            try
            {
                File.Delete(scriptPath);
            }
            catch
            {
                // Best effort.
            }

            return false;
        }
    }

    private static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
