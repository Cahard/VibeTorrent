using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace VibeTorrent.App.Services;

/// <summary>
/// One process owns the session. A second launch forwards its arguments to the
/// running instance through a per-user named pipe, which makes "Open with"
/// behave like a real desktop application.
/// </summary>
public sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = @"Local\VibeTorrent.Session.v1";
    private readonly string _pipeName;
    private readonly CancellationTokenSource _cts = new();
    private Mutex? _mutex;
    private bool _ownsMutex;
    private Task? _serverTask;
    private bool _disposed;

    public SingleInstanceCoordinator()
    {
        var user = Environment.UserName;
        var safeUser = new string(user.Where(char.IsLetterOrDigit).ToArray());
        _pipeName = $"VibeTorrent.Ipc.{safeUser}.v1";
    }

    public event Action<string[]>? ArgumentsReceived;

    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (createdNew)
        {
            _ownsMutex = true;
            return true;
        }

        try
        {
            if (_mutex.WaitOne(0))
            {
                _ownsMutex = true;
                return true;
            }
        }
        catch (AbandonedMutexException)
        {
            _ownsMutex = true;
            return true;
        }

        return false;
    }

    public bool TryForward(string[] args, TimeSpan timeout)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            client.Connect((int)Math.Max(150, timeout.TotalMilliseconds));

            using var writer = new StreamWriter(client, new UTF8Encoding(false), leaveOpen: true)
            {
                AutoFlush = true
            };
            writer.WriteLine(JsonSerializer.Serialize(args));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void StartServer()
    {
        if (_serverTask is not null || _disposed)
        {
            return;
        }

        _serverTask = Task.Run(() => ServerLoopAsync(_cts.Token));
    }

    private async Task ServerLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(line))
                {
                    ArgumentsReceived?.Invoke([]);
                    continue;
                }

                var args = JsonSerializer.Deserialize<string[]>(line) ?? [];
                ArgumentsReceived?.Invoke(args);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                // A malformed client or a transient pipe break should not kill the
                // listener. Recreate it on the next iteration.
                try
                {
                    await Task.Delay(250, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        try
        {
            _serverTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (Exception)
        {
            // Ignore shutdown races.
        }

        _cts.Dispose();
        if (_ownsMutex)
        {
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch (Exception)
            {
                // Already released by the runtime.
            }
        }

        _mutex?.Dispose();
    }
}
