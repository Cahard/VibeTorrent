using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using Microsoft.Extensions.Logging;
using VibeTorrent.App.Services;
using VibeTorrent.App.ViewModels;
using VibeTorrent.Core.Services;

namespace VibeTorrent.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<MainWindow> _logger;
    private bool _allowClose;
    private bool _exitInProgress;
    private bool _trayHintShown;
    private bool _autoDetailsHidden;
    private bool? _wasNarrow;

    public MainWindow(
        MainViewModel viewModel,
        ISettingsService settingsService,
        ILogger<MainWindow> logger)
    {
        _viewModel = viewModel;
        _settingsService = settingsService;
        _logger = logger;

        InitializeComponent();
        DataContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        var settings = settingsService.Current;
        var workArea = SystemParameters.WorkArea;
        Width = Math.Clamp(settings.WindowWidth, MinWidth, Math.Max(MinWidth, workArea.Width - 20));
        Height = Math.Clamp(settings.WindowHeight, MinHeight, Math.Max(MinHeight, workArea.Height - 20));
        if (settings.IsWindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }

        SourceInitialized += (_, _) =>
        {
            WindowEffects.ApplyWindow(this);
            WindowMaximizeFix.Attach(this);
        };
        ThemeService.ThemeChanged += OnThemeChanged;
        StateChanged += (_, _) => UpdateMaximizeGlyph();
        SizeChanged += (_, _) => ApplyResponsiveLayout();
        Loaded += OnLoaded;
    }

    public Func<Task>? ExitRequested { get; set; }

    public event Action? HiddenToTray;

    public void RefreshResponsiveLayout() => ApplyResponsiveLayout(force: true);

    public void RestoreAndActivate()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    public void HideToTray()
    {
        if (!IsVisible)
        {
            return;
        }

        Hide();
        if (!_trayHintShown)
        {
            _trayHintShown = true;
            HiddenToTray?.Invoke();
        }
    }

    public void PersistForSystemShutdown()
    {
        var width = WindowState == WindowState.Maximized && RestoreBounds.Width > 0
            ? RestoreBounds.Width
            : Width;
        var height = WindowState == WindowState.Maximized && RestoreBounds.Height > 0
            ? RestoreBounds.Height
            : Height;
        var maximized = WindowState == WindowState.Maximized;

        try
        {
            // Run the async persistence off the UI thread so waiting for the I/O
            // cannot deadlock on the WPF synchronization context.
            var persist = Task.Run(async () =>
            {
                await _viewModel.SaveUiStateAsync(width, height, maximized);
                await _viewModel.PersistSessionForShutdownAsync();
            });

            if (!persist.Wait(TimeSpan.FromSeconds(3)))
            {
                _logger.LogWarning("State persistence did not finish before session end.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not persist state during system shutdown.");
        }
    }

    public async Task RequestExitAsync()
    {
        if (_exitInProgress)
        {
            return;
        }

        _exitInProgress = true;
        try
        {
            var width = WindowState == WindowState.Maximized && RestoreBounds.Width > 0
                ? RestoreBounds.Width
                : Width;
            var height = WindowState == WindowState.Maximized && RestoreBounds.Height > 0
                ? RestoreBounds.Height
                : Height;
            await _viewModel.SaveUiStateAsync(width, height, WindowState == WindowState.Maximized);
            if (ExitRequested is not null)
            {
                await ExitRequested();
            }
            else
            {
                CloseForExit();
                Application.Current?.Shutdown();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Graceful shutdown failed.");
            CloseForExit();
            Application.Current?.Shutdown();
        }
    }

    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    private void OnThemeChanged()
    {
        Dispatcher.BeginInvoke(() => WindowEffects.ApplyWindow(this));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout();
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, fade);
    }

    private void ApplyResponsiveLayout(bool force = false)
    {
        // At 1280 px the three-column layout becomes too cramped for the detail
        // rail. Act only when crossing the breakpoint (or once after startup), so
        // a user who explicitly opens the panel at a narrow width is not fought
        // by every subsequent resize tick.
        var isNarrow = ActualWidth < 1320;
        if (force || _wasNarrow is null || isNarrow != _wasNarrow)
        {
            if (isNarrow && _viewModel.IsDetailsPaneOpen)
            {
                _autoDetailsHidden = true;
                _viewModel.IsDetailsPaneOpen = false;
            }
            else if (!isNarrow && ActualWidth >= 1360 && _autoDetailsHidden)
            {
                _autoDetailsHidden = false;
                _viewModel.IsDetailsPaneOpen = true;
            }
        }

        _wasNarrow = isNarrow;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        if (_settingsService.Current.CloseToTray)
        {
            HideToTray();
            return;
        }

        _ = RequestExitAsync();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || IsInteractiveSource(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove can race with a window state change.
        }
    }

    private void MinimizeButton_OnClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void MaximizeButton_OnClick(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_settingsService.Current.CloseToTray)
        {
            HideToTray();
        }
        else
        {
            _ = RequestExitAsync();
        }
    }

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void UpdateMaximizeGlyph()
    {
        if (MaximizeGlyph is not null)
        {
            MaximizeGlyph.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var inTextInput = Keyboard.FocusedElement is TextBoxBase;
        var onButton = Keyboard.FocusedElement is ButtonBase;
        var hasModal = _viewModel.IsAddDialogOpen || _viewModel.IsConfirmRemoveOpen;

        if (e.Key == Key.Escape)
        {
            if (_viewModel.IsConfirmRemoveOpen)
            {
                _viewModel.CancelRemoveCommand.Execute(null);
            }
            else if (_viewModel.IsAddDialogOpen)
            {
                _viewModel.CloseAddDialogCommand.Execute(null);
            }
            else if (_viewModel.IsSettingsOpen)
            {
                _viewModel.CloseSettingsCommand.Execute(null);
            }
            else if (inTextInput || _viewModel.SearchText.Length > 0)
            {
                _viewModel.ClearSearchCommand.Execute(null);
                Keyboard.ClearFocus();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.O && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !hasModal)
        {
            _viewModel.OpenAddTorrentCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !hasModal)
        {
            if (_viewModel.IsSettingsOpen)
            {
                _viewModel.CloseSettingsCommand.Execute(null);
            }

            Library.FocusSearch();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.OemComma && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !hasModal)
        {
            _viewModel.OpenSettingsCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !inTextInput && !hasModal)
        {
            _ = _viewModel.PasteMagnetFromClipboardAsync();
            e.Handled = true;
            return;
        }

        if (inTextInput || onButton || hasModal)
        {
            return;
        }

        if (e.Key == Key.Space)
        {
            _ = _viewModel.TogglePauseSelectedCommand.ExecuteAsync(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && _viewModel.SelectedTorrent is not null)
        {
            _viewModel.RequestRemoveCommand.Execute(_viewModel.SelectedTorrent);
            e.Handled = true;
        }
    }

    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (IsTextInputSource(e.OriginalSource as DependencyObject))
        {
            _viewModel.IsDragOver = false;
            return;
        }

        if (!TryReadDropData(e.Data, out var files, out var text))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var hasMagnet = !string.IsNullOrWhiteSpace(text) &&
                        text.TrimStart().StartsWith("magnet:", StringComparison.OrdinalIgnoreCase);
        var hasTorrent = files.Any(path => path.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase));
        if (!hasTorrent && !hasMagnet)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
        _viewModel.IsDragOver = true;
    }

    private void Window_PreviewDragLeave(object sender, DragEventArgs e)
    {
        _viewModel.IsDragOver = false;
    }

    private void Window_PreviewDrop(object sender, DragEventArgs e)
    {
        if (IsTextInputSource(e.OriginalSource as DependencyObject))
        {
            _viewModel.IsDragOver = false;
            return;
        }

        _viewModel.IsDragOver = false;
        e.Handled = true;
        if (!TryReadDropData(e.Data, out var files, out var text))
        {
            return;
        }

        _ = _viewModel.HandleDroppedDataAsync(files, text);
    }

    private static bool TryReadDropData(IDataObject data, out IReadOnlyList<string> files, out string? text)
    {
        files = [];
        text = null;

        try
        {
            if (data.GetDataPresent(DataFormats.FileDrop) &&
                data.GetData(DataFormats.FileDrop) is string[] dropped)
            {
                files = dropped;
            }

            if (data.GetDataPresent(DataFormats.UnicodeText))
            {
                text = data.GetData(DataFormats.UnicodeText) as string;
            }
            else if (data.GetDataPresent(DataFormats.Text))
            {
                text = data.GetData(DataFormats.Text) as string;
            }

            return files.Count > 0 || !string.IsNullOrWhiteSpace(text);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsAddDialogOpen) && _viewModel.IsAddDialogOpen)
        {
            Dispatcher.BeginInvoke(() => FocusFirst(AddOverlay));
        }
        else if (e.PropertyName == nameof(MainViewModel.IsConfirmRemoveOpen) && _viewModel.IsConfirmRemoveOpen)
        {
            Dispatcher.BeginInvoke(() => FocusFirst(ConfirmOverlay));
        }
    }

    private static void FocusFirst(DependencyObject root)
    {
        if (root is UIElement element && element.Focusable && element.IsVisible)
        {
            element.Focus();
            return;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            FocusFirst(VisualTreeHelper.GetChild(root, i));
        }
    }

    private static bool IsTextInputSource(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is TextBoxBase or PasswordBox)
            {
                return true;
            }

            source = source is Visual or Visual3D
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        return false;
    }

    private static bool IsInteractiveSource(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ButtonBase or TextBoxBase or PasswordBox)
            {
                return true;
            }

            source = source is Visual or Visual3D
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        return false;
    }
}
