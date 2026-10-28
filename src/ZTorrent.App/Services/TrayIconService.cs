using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ZTorrent.Core.Localization;
using Forms = System.Windows.Forms;

namespace ZTorrent.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly ContextMenu _menu;
    private readonly MenuItem _openItem;
    private readonly MenuItem _pauseItem;
    private readonly MenuItem _resumeItem;
    private readonly MenuItem _exitItem;
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
            Text = "ZTorrent",
            Visible = true
        };
        _icon.DoubleClick += (_, _) => Dispatch(_show);
        _icon.MouseUp += OnMouseUp;
        _openItem = new MenuItem { FontWeight = FontWeights.SemiBold };
        _pauseItem = new MenuItem();
        _resumeItem = new MenuItem();
        _exitItem = new MenuItem();
        _menu = BuildMenu();
        RefreshLanguage();
    }

    public void RefreshLanguage()
    {
        if (_disposed)
        {
            return;
        }

        _openItem.Header = L.T("Open ZTorrent");
        _pauseItem.Header = L.T("Pause all");
        _resumeItem.Header = L.T("Resume all");
        _exitItem.Header = L.T("Exit");
        _icon.Text = "ZTorrent";
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

        var safe = string.IsNullOrWhiteSpace(text) ? "ZTorrent" : text.Trim();
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
        ShowNotification(L.T("ZTorrent is minimized"),
            L.T("The client keeps running in the tray. Double-click to open the window."));
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();

        _openItem.Click += (_, _) => Dispatch(_show);
        menu.Items.Add(_openItem);
        menu.Items.Add(new Separator());

        _pauseItem.Click += (_, _) => Dispatch(_pauseAll);
        menu.Items.Add(_pauseItem);

        _resumeItem.Click += (_, _) => Dispatch(_resumeAll);
        menu.Items.Add(_resumeItem);
        menu.Items.Add(new Separator());

        _exitItem.Click += (_, _) => Dispatch(_exit);
        menu.Items.Add(_exitItem);

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
