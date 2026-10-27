using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VibeTorrent.App.Services;
using VibeTorrent.App.ViewModels;
using VibeTorrent.Core.Configuration;
using VibeTorrent.Core.Localization;
using VibeTorrent.Core.Models;
using VibeTorrent.Core.Services;

namespace VibeTorrent.App;

public partial class App : Application
{
    private ServiceProvider? _services;
    private SingleInstanceCoordinator? _singleInstance;
    private TrayIconService? _tray;
    private MainWindow? _mainWindow;
    private MainViewModel? _viewModel;
    private ILogger<App>? _logger;
    private bool _isExiting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var args = e.Args;
        var isDemo = args.Any(a => a.Equals("--demo", StringComparison.OrdinalIgnoreCase));
        var isScreenshot = args.Any(a => a.StartsWith("--screenshot", StringComparison.OrdinalIgnoreCase));

        if (!isDemo && !isScreenshot)
        {
            _singleInstance = new SingleInstanceCoordinator();
            if (!_singleInstance.TryAcquire())
            {
                var forwarded = false;
                for (var attempt = 0; attempt < 3 && !forwarded; attempt++)
                {
                    forwarded = _singleInstance.TryForward(args, TimeSpan.FromMilliseconds(800));
                    if (!forwarded)
                    {
                        Thread.Sleep(250);
                    }
                }

                if (!forwarded)
                {
                    MessageBox.Show(
                        L.T("VibeTorrent is already running but did not respond. Open it from the tray."),
                        "VibeTorrent",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                Shutdown(0);
                return;
            }

            _singleInstance.StartServer();
            _singleInstance.ArgumentsReceived += OnExternalArguments;
        }

        try
        {
            var paths = AppPaths.CreateDefault();
            paths.EnsureCreated();
            _services = BuildServices(paths);
            _logger = _services.GetRequiredService<ILogger<App>>();
            AttachExceptionHandlers();

            var settingsService = _services.GetRequiredService<ISettingsService>();
            var settings = await settingsService.LoadAsync();
            var languageOverride = GetArgumentValue(args, "--lang");
            if (!string.IsNullOrWhiteSpace(languageOverride))
            {
                settings.Language = AppLanguages.FromCode(languageOverride).ToCode();
            }

            LocalizationManager.Instance.SetLanguage(settings.Language);
            TryCreateDownloadDirectory(settings.DownloadDirectory);

            _viewModel = _services.GetRequiredService<MainViewModel>();
            _mainWindow = _services.GetRequiredService<MainWindow>();
            _mainWindow.ExitRequested = ShutdownAsync;
            _mainWindow.HiddenToTray += OnHiddenToTray;
            SessionEnding += OnSessionEnding;

            if (!isDemo)
            {
                _tray = BuildTray();
                _viewModel.NotificationRequested += OnNotificationRequested;
                LocalizationManager.Instance.LanguageChanged += (_, _) => _tray?.RefreshLanguage();
            }

            var startHidden = !isDemo && !isScreenshot &&
                              (settings.StartMinimized ||
                               args.Contains("--minimized", StringComparer.OrdinalIgnoreCase));

            if (!startHidden)
            {
                _mainWindow.Show();
            }

            await _viewModel.InitializeAsync(settings, args, isDemo);
            if (!startHidden)
            {
                _mainWindow.RefreshResponsiveLayout();
            }

            var screenshotTarget = GetArgumentValue(args, "--screenshot");
            var screenshotsDirectory = GetArgumentValue(args, "--screenshots");
            if (!string.IsNullOrWhiteSpace(screenshotsDirectory))
            {
                await CaptureScreensAsync(screenshotsDirectory, multiple: true);
                ExitAfterScreenshot();
                return;
            }

            if (!string.IsNullOrWhiteSpace(screenshotTarget))
            {
                await CaptureScreensAsync(screenshotTarget, multiple: false);
                ExitAfterScreenshot();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogCritical(ex, "Fatal startup error.");
            MessageBox.Show(
                L.F("VibeTorrent could not start.\n\n{0}\n\nLog: {1}", ex.Message,
                    AppPaths.CreateDefault().LogsDirectory),
                "VibeTorrent",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    public async Task ShutdownAsync()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        try
        {
            if (_viewModel is not null)
            {
                await _viewModel.ShutdownAsync();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "ViewModel shutdown failed.");
        }
        finally
        {
            _tray?.Dispose();
            _tray = null;
            _singleInstance?.Dispose();
            _singleInstance = null;
            _mainWindow?.CloseForExit();
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _singleInstance?.Dispose();
        _services?.Dispose();
        base.OnExit(e);
    }

    private ServiceProvider BuildServices(AppPaths paths)
    {
        var services = new ServiceCollection();
        services.AddSingleton(paths);
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddDebug();
            builder.AddProvider(new FileLoggerProvider(paths.LogsDirectory));
        });

        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ISessionStore, SessionStore>();
        services.AddSingleton<ITorrentPreviewService, TorrentPreviewService>();
        services.AddSingleton<ITorrentEngineService, TorrentEngineService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
        services.AddSingleton<IStartupService, StartupService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        return services.BuildServiceProvider();
    }

    private TrayIconService BuildTray()
    {
        return new TrayIconService(
            show: () => Dispatcher.BeginInvoke(() => _mainWindow?.RestoreAndActivate()),
            pauseAll: () => Dispatcher.BeginInvoke(async () =>
            {
                if (_viewModel is not null)
                {
                    await _viewModel.PauseAllCommand.ExecuteAsync(null);
                }
            }),
            resumeAll: () => Dispatcher.BeginInvoke(async () =>
            {
                if (_viewModel is not null)
                {
                    await _viewModel.ResumeAllCommand.ExecuteAsync(null);
                }
            }),
            exit: () => Dispatcher.BeginInvoke(async () =>
            {
                if (_mainWindow is not null)
                {
                    await _mainWindow.RequestExitAsync();
                }
            }));
    }

    private void OnExternalArguments(string[] args)
    {
        Dispatcher.BeginInvoke(async () =>
        {
            _mainWindow?.RestoreAndActivate();
            if (_viewModel is not null && args.Length > 0)
            {
                await _viewModel.ProcessExternalArgumentsAsync(args);
            }
        });
    }

    private void OnHiddenToTray()
    {
        _tray?.ShowTrayHint();
    }

    private void OnSessionEnding(object? sender, SessionEndingCancelEventArgs e)
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        try
        {
            _mainWindow?.PersistForSystemShutdown();
            var stop = Task.Run(async () =>
            {
                if (_viewModel is not null)
                {
                    await _viewModel.ShutdownAsync();
                }
            });
            stop.Wait(TimeSpan.FromSeconds(4));
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Best-effort shutdown on session end failed.");
        }

        _tray?.Dispose();
        _tray = null;
        _singleInstance?.Dispose();
        _singleInstance = null;
    }

    private void OnNotificationRequested(string title, string message, bool isError)
    {
        _tray?.ShowNotification(title, message, isError);
    }

    private async Task CaptureScreensAsync(string target, bool multiple)
    {
        if (_mainWindow is null || _viewModel is null)
        {
            return;
        }

        await Task.Delay(1100);
        _mainWindow.UpdateLayout();

        if (!multiple)
        {
            ScreenshotService.Capture(_mainWindow, target);
            return;
        }

        Directory.CreateDirectory(target);
        ScreenshotService.Capture(_mainWindow, Path.Combine(target, "01-library.png"));

        _viewModel.OpenMagnetCommand.Execute(null);
        if (_viewModel.AddDialog is not null)
        {
            _viewModel.AddDialog.MagnetText =
                "magnet:?xt=urn:btih:4f2c8a1d9b3e5f60718293a4b5c6d7e8f9012345" +
                "&dn=NeuroSymphony%20%E2%80%94%20Original%20Soundtrack%20%5BFLAC%5D" +
                "&xl=4963000000" +
                "&tr=udp%3A%2F%2Ftracker.opentrackr.org%3A1337%2Fannounce";
        }

        await Task.Delay(700);
        ScreenshotService.Capture(_mainWindow, Path.Combine(target, "02-add-torrent.png"));
        _viewModel.CloseAddDialogCommand.Execute(null);

        _viewModel.OpenSettingsCommand.Execute(null);
        await Task.Delay(500);
        ScreenshotService.Capture(_mainWindow, Path.Combine(target, "03-settings.png"));
        _viewModel.CloseSettingsCommand.Execute(null);

        _viewModel.OpenAddTorrentCommand.Execute(null);
        if (_viewModel.AddDialog is not null)
        {
            _viewModel.AddDialog.TorrentPath = @"D:\Downloads\NeuroSymphony.torrent";
            _viewModel.AddDialog.Preview = new TorrentPreview
            {
                Name = "NeuroSymphony — Original Soundtrack [FLAC]",
                InfoHash = "4f2c8a1d9b3e5f60718293a4b5c6d7e8f9012345",
                TotalSize = 4_963_000_000,
                HasMetadata = true,
                Files =
                [
                    new PreviewFile { Path = "01 — Opening.flac", Length = 148_000_000 },
                    new PreviewFile { Path = "02 — Neural Dawn.flac", Length = 132_000_000 },
                    new PreviewFile { Path = "03 — Afterglow.flac", Length = 126_000_000 },
                    new PreviewFile { Path = "Cover — front.png", Length = 18_000_000 },
                    new PreviewFile { Path = "Booklet.pdf", Length = 34_000_000 },
                    new PreviewFile { Path = "checksums.sha256", Length = 4_096 }
                ],
                Trackers = ["udp://tracker.opentrackr.org:1337/announce"]
            };
        }

        await Task.Delay(450);
        ScreenshotService.Capture(_mainWindow, Path.Combine(target, "04-add-file.png"));
        _viewModel.CloseAddDialogCommand.Execute(null);

        if (_viewModel.Details is not null)
        {
            _viewModel.Details.SelectedTabIndex = 1;
        }

        await Task.Delay(350);
        ScreenshotService.Capture(_mainWindow, Path.Combine(target, "05-files-tab.png"));

        if (_viewModel.SelectedTorrent is not null)
        {
            _viewModel.RequestRemoveCommand.Execute(_viewModel.SelectedTorrent);
            await Task.Delay(300);
            ScreenshotService.Capture(_mainWindow, Path.Combine(target, "06-confirm-remove.png"));
            _viewModel.CancelRemoveCommand.Execute(null);
        }
    }

    private void ExitAfterScreenshot()
    {
        _isExiting = true;
        _tray?.Dispose();
        _tray = null;
        _singleInstance?.Dispose();
        _singleInstance = null;
        _mainWindow?.CloseForExit();
        Shutdown(0);
    }

    private static void TryCreateDownloadDirectory(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path))
            {
                Directory.CreateDirectory(path);
            }
        }
        catch (Exception)
        {
            // The client still opens; adding a torrent will surface a useful error.
        }
    }

    private static string? GetArgumentValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals(name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return args[i + 1];
            }

            if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                return arg[(name.Length + 1)..];
            }
        }

        return null;
    }

    private void AttachExceptionHandlers()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            _logger?.LogCritical(e.Exception, "Unhandled dispatcher exception.");
            if (_isExiting)
            {
                return;
            }

            MessageBox.Show(
                L.F("An unexpected error occurred.\n\n{0}\n\nDetails were saved to the log.", e.Exception.Message),
                "VibeTorrent",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            e.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            _logger?.LogError(e.Exception, "Unobserved task exception.");
            e.SetObserved();
        };
    }
}
