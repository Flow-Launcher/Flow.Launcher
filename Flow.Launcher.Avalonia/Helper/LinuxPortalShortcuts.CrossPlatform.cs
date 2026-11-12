using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Flow.Launcher.Infrastructure.Logger;
using Tmds.DBus.Protocol;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// Global hotkeys on Wayland through the xdg-desktop-portal GlobalShortcuts interface. Wayland gives clients no way
/// to grab keys; the compositor owns the shortcuts and only reports activations of the ones an app has bound.
/// <para>
/// The portal has no way to drop a single shortcut, so every change closes the session and binds the whole set in a
/// new one (changes are coalesced). Binding is asynchronous and may show a confirmation dialog (GNOME, KDE); on
/// compositors that take the key from their own configuration (Hyprland) the user binds the shortcut id there.
/// Shortcut ids are derived from the hotkey ("Alt + Space" → "alt-space").
/// </para>
/// </summary>
internal sealed class LinuxPortalShortcuts : IDisposable
{
    private static readonly string ClassName = nameof(LinuxPortalShortcuts);

    private const string PortalService = "org.freedesktop.portal.Desktop";
    private const string PortalPath = "/org/freedesktop/portal/desktop";
    private const string GlobalShortcutsInterface = "org.freedesktop.portal.GlobalShortcuts";
    private const string RequestInterface = "org.freedesktop.portal.Request";
    private const string SessionInterface = "org.freedesktop.portal.Session";
    private const string RegistryInterface = "org.freedesktop.host.portal.Registry";

    private static readonly TimeSpan RebindDelay = TimeSpan.FromMilliseconds(200);

    private sealed record Shortcut(string Id, string Trigger, string Description, Action Callback);

    private readonly DBusConnection _connection;
    private readonly IDisposable _activatedWatcher;
    private readonly Lock _lock = new();
    private readonly Dictionary<int, Shortcut> _shortcuts = new();
    private int _nextId;
    private bool _dirty;
    private bool _rebinding;
    private string? _sessionHandle;
    private volatile bool _disposed;

    private LinuxPortalShortcuts(DBusConnection connection, IDisposable activatedWatcher)
    {
        _connection = connection;
        _activatedWatcher = activatedWatcher;
    }

    /// <summary>
    /// Connects to the session bus; null when no portal implements GlobalShortcuts.
    /// </summary>
    internal static async Task<LinuxPortalShortcuts?> TryCreateAsync(bool desktopEntryJustInstalled)
    {
        if (DBusAddress.Session is not { } address)
        {
            Log.Warn(ClassName, "No D-Bus session bus");
            return null;
        }

        var connection = new DBusConnection(address);
        try
        {
            await connection.ConnectAsync().ConfigureAwait(false);

            // Unsandboxed apps have no app id unless they register one before their first portal call. The desktop
            // remembers bound shortcuts by that id, and recent xdg-desktop-portal releases refuse GlobalShortcuts
            // sessions without one. Registering needs xdg-desktop-portal 1.19+ and an installed desktop entry.
            if (!await TryRegisterAppIdAsync(connection).ConfigureAwait(false) && desktopEntryJustInstalled)
            {
                // The portal's desktop entry index notices a freshly installed entry through inotify, which can lag
                // behind the write.
                await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                await TryRegisterAppIdAsync(connection).ConfigureAwait(false);
            }

            var version = await GetVersionAsync(connection).ConfigureAwait(false);
            if (version is null)
            {
                connection.Dispose();
                return null;
            }

            LinuxPortalShortcuts? instance = null;
            var watcher = await connection.WatchSignalAsync(null, PortalPath, GlobalShortcutsInterface, "Activated",
                static (message, _) =>
                {
                    var reader = message.GetBodyReader();
                    var sessionHandle = reader.ReadObjectPathAsString();
                    var shortcutId = reader.ReadString();
                    return (sessionHandle, shortcutId);
                },
                notification =>
                {
                    if (notification.Type == NotificationType.Value)
                    {
                        instance?.OnActivated(notification.Value.sessionHandle, notification.Value.shortcutId);
                    }
                },
                ObserverFlags.None, false, null).ConfigureAwait(false);

            instance = new LinuxPortalShortcuts(connection, watcher);
            Log.Info(ClassName, $"Using the GlobalShortcuts portal (version {version})");
            return instance;
        }
        catch (Exception e)
        {
            Log.Warn(ClassName, $"GlobalShortcuts portal unavailable: {e.Message}");
            connection.Dispose();
            return null;
        }
    }

    /// <summary>
    /// The portal shortcut id for a hotkey string, e.g. "Ctrl + Alt + Space" → "ctrl-alt-space".
    /// </summary>
    internal static string ToShortcutId(string hotkeyString) =>
        string.Join('-', hotkeyString.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToLowerInvariant();

    /// <summary>
    /// Adds a shortcut and schedules binding. Returns -1 when the id is already bound by another hotkey.
    /// </summary>
    internal int Register(string shortcutId, string trigger, string description, Action callback)
    {
        lock (_lock)
        {
            if (_shortcuts.Values.Any(shortcut => shortcut.Id == shortcutId))
            {
                return -1;
            }

            var id = _nextId++;
            _shortcuts[id] = new Shortcut(shortcutId, trigger, description, callback);
            ScheduleRebind();
            return id;
        }
    }

    internal bool Unregister(int id)
    {
        lock (_lock)
        {
            if (_shortcuts.Remove(id))
            {
                ScheduleRebind();
            }

            return true;
        }
    }

    private void OnActivated(string sessionHandle, string shortcutId)
    {
        Action? callback;
        lock (_lock)
        {
            if (sessionHandle != _sessionHandle)
            {
                return;
            }

            callback = _shortcuts.Values.FirstOrDefault(shortcut => shortcut.Id == shortcutId)?.Callback;
        }

        if (callback != null)
        {
            Dispatcher.UIThread.Post(callback);
        }
    }

    // Called with _lock held.
    private void ScheduleRebind()
    {
        _dirty = true;
        if (_rebinding)
        {
            return;
        }

        _rebinding = true;
        _ = Task.Run(RebindLoopAsync);
    }

    private async Task RebindLoopAsync()
    {
        // Startup registers the toggle hotkey and every custom query hotkey in a row; bind them in one session.
        await Task.Delay(RebindDelay).ConfigureAwait(false);

        while (!_disposed)
        {
            Shortcut[] shortcuts;
            lock (_lock)
            {
                if (!_dirty)
                {
                    _rebinding = false;
                    return;
                }

                _dirty = false;
                shortcuts = _shortcuts.Values.ToArray();
            }

            try
            {
                await RebindAsync(shortcuts).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                // Portal failures are environmental (portal restarted, request denied); keep the loop alive.
                Log.Error(ClassName, $"Binding global shortcuts through the portal failed: {e}");
            }
        }
    }

    private async Task RebindAsync(Shortcut[] shortcuts)
    {
        await CloseSessionAsync().ConfigureAwait(false);
        if (shortcuts.Length == 0)
        {
            return;
        }

        var sessionToken = NewToken();
        var (createResponse, createResults) = await RequestAsync("CreateSession", "a{sv}", (ref MessageWriter writer, string handleToken) =>
            writer.WriteDictionary(new Dictionary<string, VariantValue>
            {
                ["handle_token"] = handleToken,
                ["session_handle_token"] = sessionToken,
            })).ConfigureAwait(false);

        if (createResponse != 0 || !createResults.TryGetValue("session_handle", out var handleValue))
        {
            Log.Error(ClassName, $"GlobalShortcuts CreateSession failed (response {createResponse})");
            return;
        }

        var sessionHandle = handleValue.Type == VariantValueType.ObjectPath ? handleValue.GetObjectPathAsString() : handleValue.GetString();
        lock (_lock)
        {
            _sessionHandle = sessionHandle;
        }

        var (bindResponse, bindResults) = await RequestAsync("BindShortcuts", "oa(sa{sv})sa{sv}", (ref MessageWriter writer, string handleToken) =>
        {
            writer.WriteObjectPath(sessionHandle);
            var array = writer.WriteArrayStart(DBusType.Struct);
            foreach (var shortcut in shortcuts)
            {
                writer.WriteStructureStart();
                writer.WriteString(shortcut.Id);
                writer.WriteDictionary(new Dictionary<string, VariantValue>
                {
                    ["description"] = shortcut.Description,
                    ["preferred_trigger"] = shortcut.Trigger,
                });
            }

            writer.WriteArrayEnd(array);
            writer.WriteString(string.Empty); // parent window: none (Avalonia runs on XWayland, no xdg-foreign handle)
            writer.WriteDictionary(new Dictionary<string, VariantValue> { ["handle_token"] = handleToken });
        }).ConfigureAwait(false);

        if (bindResponse != 0)
        {
            Log.Warn(ClassName, $"GlobalShortcuts BindShortcuts was declined or failed (response {bindResponse})");
            return;
        }

        Log.Info(ClassName, $"Bound global shortcuts: {DescribeBound(bindResults)}");
    }

    private static string DescribeBound(Dictionary<string, VariantValue> results)
    {
        if (!results.TryGetValue("shortcuts", out var bound) || bound.Type != VariantValueType.Array)
        {
            return "(none reported)";
        }

        var descriptions = new List<string>();
        for (var i = 0; i < bound.Count; i++)
        {
            var entry = bound.GetItem(i);
            var id = entry.GetItem(0).GetString();
            var properties = entry.GetItem(1);
            var trigger = "unassigned";
            for (var j = 0; j < properties.Count; j++)
            {
                var property = properties.GetDictionaryEntry(j);
                if (property.Key.GetString() == "trigger_description")
                {
                    var value = property.Value.Type == VariantValueType.Variant ? property.Value.GetVariantValue() : property.Value;
                    trigger = value.GetString() is { Length: > 0 } text ? text : trigger;
                }
            }

            descriptions.Add($"{id} ({trigger})");
        }

        return descriptions.Count == 0 ? "(none)" : string.Join(", ", descriptions);
    }

    private async Task CloseSessionAsync()
    {
        string? sessionHandle;
        lock (_lock)
        {
            sessionHandle = _sessionHandle;
            _sessionHandle = null;
        }

        if (sessionHandle is null)
        {
            return;
        }

        try
        {
            await _connection.CallMethodAsync(CreateCall(_connection, sessionHandle, SessionInterface, "Close", null, null))
                .ConfigureAwait(false);
        }
        catch (DBusErrorReplyException e)
        {
            // The session may already be gone (portal restarted).
            Log.Debug(ClassName, $"Closing portal session failed: {e.ErrorName}: {e.ErrorMessage}");
        }
    }

    /// <summary>
    /// Calls a GlobalShortcuts method that returns an org.freedesktop.portal.Request and waits for its Response.
    /// The Response watcher is installed before the call so a fast reply cannot be missed.
    /// </summary>
    private async Task<(uint Response, Dictionary<string, VariantValue> Results)> RequestAsync(string method, string signature,
        WriteRequestArguments writeArguments)
    {
        var handleToken = NewToken();
        var sender = _connection.UniqueName!.TrimStart(':').Replace('.', '_');
        var requestPath = $"/org/freedesktop/portal/desktop/request/{sender}/{handleToken}";

        var response = new TaskCompletionSource<(uint, Dictionary<string, VariantValue>)>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher = await _connection.WatchSignalAsync(null, requestPath, RequestInterface, "Response",
            static (message, _) =>
            {
                var reader = message.GetBodyReader();
                var code = reader.ReadUInt32();
                var results = reader.ReadDictionaryOfStringToVariantValue();
                return (code, results);
            },
            notification =>
            {
                if (notification.Type == NotificationType.Value)
                {
                    response.TrySetResult(notification.Value);
                }
                else
                {
                    // The watcher was disposed or the connection dropped before the portal answered.
                    response.TrySetException(notification.Exception
                                             ?? new DBusConnectionClosedException($"Portal request {method} ended without a response ({notification.Type})"));
                }
            },
            ObserverFlags.None, false, null).ConfigureAwait(false);

        await _connection.CallMethodAsync(CreateCall(_connection, PortalPath, GlobalShortcutsInterface, method, signature,
            (ref MessageWriter writer) => writeArguments(ref writer, handleToken))).ConfigureAwait(false);

        return await response.Task.ConfigureAwait(false);
    }

    // MessageWriter is a ref struct: argument writers must take it by reference, a copy would lose what they write.
    private delegate void WriteArguments(ref MessageWriter writer);

    private delegate void WriteRequestArguments(ref MessageWriter writer, string handleToken);

    private static MessageBuffer CreateCall(DBusConnection connection, string path, string @interface, string member,
        string? signature, WriteArguments? writeArguments)
    {
        var writer = connection.GetMessageWriter();
        try
        {
            writer.WriteMethodCallHeader(PortalService, path, @interface, member, signature, MessageFlags.None);
            writeArguments?.Invoke(ref writer);
            return writer.CreateMessage();
        }
        finally
        {
            writer.Dispose();
        }
    }

    private static async Task<bool> TryRegisterAppIdAsync(DBusConnection connection)
    {
        try
        {
            await connection.CallMethodAsync(CreateCall(connection, PortalPath, RegistryInterface, "Register", "sa{sv}",
                static (ref MessageWriter writer) =>
                {
                    writer.WriteString(LinuxDesktopEntry.AppId);
                    writer.WriteDictionary(new Dictionary<string, VariantValue>());
                })).ConfigureAwait(false);
            return true;
        }
        catch (DBusErrorReplyException e)
        {
            Log.Warn(ClassName, $"Portal app id registration failed ({e.ErrorName}: {e.ErrorMessage})");
            return false;
        }
    }

    private static async Task<uint?> GetVersionAsync(DBusConnection connection)
    {
        var message = CreateCall(connection, PortalPath, "org.freedesktop.DBus.Properties", "Get", "ss",
            static (ref MessageWriter writer) =>
            {
                writer.WriteString(GlobalShortcutsInterface);
                writer.WriteString("version");
            });

        try
        {
            return await connection.CallMethodAsync(message, static (reply, _) => reply.GetBodyReader().ReadVariantValue().GetUInt32(), null)
                .ConfigureAwait(false);
        }
        catch (DBusErrorReplyException e)
        {
            Log.Info(ClassName, $"No GlobalShortcuts portal ({e.ErrorName}: {e.ErrorMessage})");
            return null;
        }
    }

    private static string NewToken() => "flow" + Guid.NewGuid().ToString("N");

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_lock)
        {
            _shortcuts.Clear();
        }

        try
        {
            // Closing the session releases the shortcuts right away instead of when the bus connection drops.
            CloseSessionAsync().Wait(TimeSpan.FromSeconds(1));
        }
        catch (Exception e)
        {
            Log.Debug(ClassName, $"Closing portal session on shutdown failed: {e.Message}");
        }

        _activatedWatcher.Dispose();
        _connection.Dispose();
    }
}
