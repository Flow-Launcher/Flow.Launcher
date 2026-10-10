using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// Per-user single instance guard. The first instance holds an exclusive lock file in the data directory and
/// listens on a named pipe; later launches connect to the pipe to ask the first instance to show itself and exit.
/// </summary>
public static class SingleInstance
{
    private const string ShowMessage = "show";

    private static readonly string PipeName = $"FlowLauncher_{Environment.UserName}_SingleInstance";

    private static FileStream? _lockFile;
    private static CancellationTokenSource? _cts;
    private static int _released;

    /// <summary>
    /// Tries to become the first instance. When another instance is running, signals it to show its window and returns false.
    /// </summary>
    public static bool TryAcquire()
    {
        try
        {
            var dir = DataLocation.DataDirectory();
            Directory.CreateDirectory(dir);
            _lockFile = new FileStream(Path.Combine(dir, ".instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            SignalFirstInstance();
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            SignalFirstInstance();
            return false;
        }
    }

    /// <summary>
    /// Starts listening for activation requests from later launches. <paramref name="onSecondInstance"/> runs on a thread-pool thread.
    /// </summary>
    public static void StartListening(Action onSecondInstance)
    {
        if (_lockFile is null || _cts is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(token);
                    using var reader = new StreamReader(server);
                    var message = await reader.ReadLineAsync(token);
                    if (message == ShowMessage)
                    {
                        onSecondInstance();
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception)
                {
                    // A broken client connection must not stop the listener; back off briefly to avoid spinning.
                    try { await Task.Delay(500, token); } catch (OperationCanceledException) { return; }
                }
            }
        }, token);
    }

    /// <summary>
    /// Stops the listener and releases the lock. Idempotent.
    /// </summary>
    public static void Release()
    {
        if (Interlocked.Exchange(ref _released, 1) != 0)
        {
            return;
        }

        try { _cts?.Cancel(); } catch (ObjectDisposedException) { }
        _lockFile?.Dispose();
        _lockFile = null;
    }

    private static void SignalFirstInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(3000);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(ShowMessage);
        }
        catch (Exception)
        {
            // First instance is not reachable (e.g. still starting); nothing more to do.
        }
    }
}
