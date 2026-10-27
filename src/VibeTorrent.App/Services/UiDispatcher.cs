using System.Windows;
using System.Windows.Threading;

namespace VibeTorrent.App.Services;

public interface IUiDispatcher
{
    bool IsOnUiThread { get; }

    void Post(Action action);

    Task InvokeAsync(Action action);

    Task<T> InvokeAsync<T>(Func<T> action);
}

public sealed class WpfUiDispatcher : IUiDispatcher
{
    private static Dispatcher Dispatcher =>
        Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

    public bool IsOnUiThread => Dispatcher.CheckAccess();

    public void Post(Action action)
    {
        if (IsOnUiThread)
        {
            action();
        }
        else
        {
            Dispatcher.BeginInvoke(action, DispatcherPriority.Normal);
        }
    }

    public Task InvokeAsync(Action action) =>
        IsOnUiThread
            ? RunOnUi(action)
            : Dispatcher.InvokeAsync(action, DispatcherPriority.Normal).Task;

    public Task<T> InvokeAsync<T>(Func<T> action)
    {
        if (IsOnUiThread)
        {
            try
            {
                return Task.FromResult(action());
            }
            catch (Exception ex)
            {
                return Task.FromException<T>(ex);
            }
        }

        return Dispatcher.InvokeAsync(action, DispatcherPriority.Normal).Task;
    }

    private static Task RunOnUi(Action action)
    {
        try
        {
            action();
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }
}
