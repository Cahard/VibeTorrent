using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Forms = System.Windows.Forms;

namespace VibeTorrent.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly ContextMenu _menu;
    private readonly Action _show;
    private readonly Action _pauseAll;
    private readonly Action _resumeAll;
    private readonly Action _exit;
    private bool _disposed;
    private bool _hintShown;

    public TrayIconService(Action show, Action pauseAll, Action resumeAll, Action exit)
    {
        _show = show;
        _pauseAll = pauseAll;
        _resumeAll = resumeAll;
        _exit = exit;

        var iconPath = Environment.ProcessPath;
        var icon = !string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath)
            ? Icon.ExtractAssociatedIcon(iconPath)
            : null;
        _icon = new Forms.NotifyIcon
        {
            Icon = icon ?? SystemIcons.Application,
            Text = "VibeTorrent",
            Visible = true
        };
        _icon.DoubleClick += (_, _) => Dispatch(_show);
        _icon.MouseUp += OnMouseUp;
        _menu = BuildMenu();
    }

    public void ShowNotification(string title, string message, bool isError = false)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _icon.BalloonTipTitle = title;
            _icon.BalloonTipText = message;
            _icon.BalloonTipIcon = isError ? Forms.ToolTipIcon.Warning : Forms.ToolTipIcon.Info;
            _icon.ShowBalloonTip(4500);
        }
        catch (Exception)
        {
            // Notifications are optional; a missing shell tray must not crash.
        }
    }

    public void UpdateTooltip(string text)
    {
        if (_disposed)
        {
            return;
        }

        var safe = string.IsNullOrWhiteSpace(text) ? "VibeTorrent" : text.Trim();
        _icon.Text = safe.Length > 62 ? safe[..61] + "…" : safe;
    }

    public void HideHint()
    {
        if (_hintShown)
        {
            _icon.Visible = true;
        }
    }

    public void ShowTrayHint()
    {
        if (_hintShown || _disposed)
        {
            return;
        }

        _hintShown = true;
        ShowNotification("VibeTorrent свёрнут", "Клиент продолжает работать в трее. Двойной клик — открыть окно.");
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();

        var open = new MenuItem { Header = "Открыть VibeTorrent", FontWeight = FontWeights.SemiBold };
        open.Click += (_, _) => Dispatch(_show);
        menu.Items.Add(open);
        menu.Items.Add(new Separator());

        var pause = new MenuItem { Header = "Пауза для всех" };
        pause.Click += (_, _) => Dispatch(_pauseAll);
        menu.Items.Add(pause);

        var resume = new MenuItem { Header = "Возобновить все" };
        resume.Click += (_, _) => Dispatch(_resumeAll);
        menu.Items.Add(resume);
        menu.Items.Add(new Separator());

        var exit = new MenuItem { Header = "Выход" };
        exit.Click += (_, _) => Dispatch(_exit);
        menu.Items.Add(exit);

        return menu;
    }

    private void OnMouseUp(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left && e.Clicks >= 2)
        {
            Dispatch(_show);
            return;
        }

        if (e.Button != Forms.MouseButtons.Right)
        {
            return;
        }

        Dispatch(() =>
        {
            _menu.Placement = PlacementMode.MousePoint;
            _menu.IsOpen = true;
        });
    }

    private static void Dispatch(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _menu.IsOpen = false;
        _icon.Visible = false;
        _icon.Dispose();
    }
}
