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
/// Launching with <see cref="ToggleArgument"/> asks it to toggle the search window instead, so desktops without a
/// global hotkey backend (e.g. Wayland compositors without the GlobalShortcuts portal) can bind the command to a key.
/// </summary>
public static class SingleInstance
{
    public const string ToggleArgument = "--toggle";

    private const string ShowMessage = "show";
    private const string ToggleMessage = "toggle";

    private static readonly string PipeName = CreatePipeName();

    // On Unix .NET binds the pipe as a socket in the temp directory, but TMPDIR differs between launch environments
    // (nix develop, systemd units, desktop launchers), so a second launch would miss the first instance. A rooted name
    // is used as the socket path as is; the per-user runtime directory is the same for every process of the session.
    private static string CreatePipeName()
    {
        var name = $"FlowLauncher_{Environment.UserName}_SingleInstance";
        return OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is { Length: > 0 } runtimeDirectory
            ? Path.Combine(runtimeDirectory, name)
            : name;
    }

    private static FileStream? _lockFile;
    private static CancellationTokenSource? _cts;
    private static int _released;

    /// <summary>
    /// Tries to become the first instance. When another instance is running, signals it to show (or, with
    /// <see cref="ToggleArgument"/> in <paramref name="args"/>, toggle) its window and returns false.
    /// </summary>
    public static bool TryAcquire(string[] args)
    {
        try
        {
            var dir = DataLocation.DataDirectory();
            Directory.CreateDirectory(dir);
            _lockFile = new FileStream(Path.Combine(dir, ".instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SignalFirstInstance(Array.IndexOf(args, ToggleArgument) >= 0 ? ToggleMessage : ShowMessage);
            return false;
        }
    }

    /// <summary>
    /// Starts listening for requests from later launches. The callbacks run on a thread-pool thread.
    /// </summary>
    public static void StartListening(Action onShow, Action onToggle)
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
                    switch (message)
                    {
                        case ShowMessage:
                            onShow();
                            break;
                        case ToggleMessage:
                            onToggle();
                            break;
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

    private static void SignalFirstInstance(string message)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(3000);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(message);
        }
        catch (Exception)
        {
            // First instance is not reachable (e.g. still starting); nothing more to do.
        }
    }
}
