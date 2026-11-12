using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Tmds.DBus.Protocol;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// Shows Flow's notifications through the desktop's notification server (org.freedesktop.Notifications: GNOME,
/// KDE, mako, dunst, swaync, ...), so they look and stack like every other notification and follow its placement
/// and do-not-disturb settings, instead of as Flow windows that tiling compositors would tile.
/// </summary>
internal static class LinuxDesktopNotifications
{
    private static readonly string ClassName = nameof(LinuxDesktopNotifications);

    private const string Service = "org.freedesktop.Notifications";
    private const string ObjectPath = "/org/freedesktop/Notifications";
    private const string Interface = "org.freedesktop.Notifications";
    private const string ActionKey = "flow-action";
    private const int ExpireTimeoutMilliseconds = 5000;

    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private static readonly ConcurrentDictionary<uint, Action> PendingActions = new();
    private static Task<Server?>? _server;

    private sealed record Server(DBusConnection Connection, bool BodyMarkup, bool Actions);

    /// <summary>
    /// Sends the notification; false when no notification server is reachable, so the caller can fall back.
    /// </summary>
    internal static async Task<bool> TryShowAsync(string title, string subTitle, string iconPath, string? actionButtonText,
        Action? buttonAction)
    {
        var server = await GetServerAsync().ConfigureAwait(false);
        if (server is null)
        {
            return false;
        }

        var hasAction = server.Actions && buttonAction != null && !string.IsNullOrWhiteSpace(actionButtonText);
        var icon = File.Exists(iconPath) ? Path.GetFullPath(iconPath) : Path.Combine(Constant.ProgramDirectory, "Images", "app.png");
        var body = server.BodyMarkup ? EscapeMarkup(subTitle) : subTitle;

        try
        {
            var id = await server.Connection.CallMethodAsync(
                CreateNotifyMessage(server.Connection, title, body, icon, hasAction ? actionButtonText! : null),
                static (message, _) => message.GetBodyReader().ReadUInt32(), null).ConfigureAwait(false);

            if (hasAction)
            {
                PendingActions[id] = buttonAction!;
            }

            return true;
        }
        catch (DBusErrorReplyException e)
        {
            Log.Warn(ClassName, $"Notify failed ({e.ErrorName}: {e.ErrorMessage})");
            return false;
        }
    }

    private static async Task<Server?> GetServerAsync()
    {
        await InitLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _server ??= ConnectAsync();
        }
        finally
        {
            InitLock.Release();
        }

        return await _server.ConfigureAwait(false);
    }

    private static async Task<Server?> ConnectAsync()
    {
        try
        {
            if (DBusConnection.Session is not { } connection)
            {
                Log.Warn(ClassName, "No D-Bus session bus; using Flow's notification windows");
                return null;
            }

            var capabilities = await connection.CallMethodAsync(CreateCall(connection, "GetCapabilities", null, null),
                static (message, _) => message.GetBodyReader().ReadArrayOfString(), null).ConfigureAwait(false);

            await connection.WatchSignalAsync(Service, ObjectPath, Interface, "ActionInvoked",
                static (message, _) =>
                {
                    var reader = message.GetBodyReader();
                    return (Id: reader.ReadUInt32(), Key: reader.ReadString());
                },
                static notification =>
                {
                    if (notification.Type == NotificationType.Value && notification.Value.Key == ActionKey
                        && PendingActions.TryRemove(notification.Value.Id, out var action))
                    {
                        Dispatcher.UIThread.Post(() => RunAction(action));
                    }
                },
                ObserverFlags.None, false, null).ConfigureAwait(false);

            await connection.WatchSignalAsync(Service, ObjectPath, Interface, "NotificationClosed",
                static (message, _) => message.GetBodyReader().ReadUInt32(),
                static notification =>
                {
                    if (notification.Type == NotificationType.Value)
                    {
                        PendingActions.TryRemove(notification.Value, out _);
                    }
                },
                ObserverFlags.None, false, null).ConfigureAwait(false);

            var server = new Server(connection, Array.IndexOf(capabilities, "body-markup") >= 0,
                Array.IndexOf(capabilities, "actions") >= 0);
            Log.Info(ClassName, $"Using the desktop notification server (capabilities: {string.Join(", ", capabilities)})");
            return server;
        }
        catch (Exception e) when (e is DBusErrorReplyException or DBusConnectionException)
        {
            Log.Warn(ClassName, $"No desktop notification server ({e.Message}); using Flow's notification windows");
            return null;
        }
    }

    private static void RunAction(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Log.Exception(ClassName, "Notification action failed", e);
        }
    }

    private static MessageBuffer CreateNotifyMessage(DBusConnection connection, string title, string body, string icon,
        string? actionButtonText) =>
        CreateCall(connection, "Notify", "susssasa{sv}i", (ref MessageWriter writer) =>
        {
            writer.WriteString("Flow Launcher");
            writer.WriteUInt32(0); // replaces_id: a new notification
            writer.WriteString(icon);
            writer.WriteString(title);
            writer.WriteString(body);
            writer.WriteArray(actionButtonText is null ? Array.Empty<string>() : new[] { ActionKey, actionButtonText });
            writer.WriteDictionary(new Dictionary<string, VariantValue>
            {
                // Lets the server show the app's name and icon and apply per-app settings.
                ["desktop-entry"] = Program.LinuxAppId,
            });
            writer.WriteInt32(ExpireTimeoutMilliseconds);
        });

    private delegate void WriteArguments(ref MessageWriter writer);

    private static MessageBuffer CreateCall(DBusConnection connection, string member, string? signature,
        WriteArguments? writeArguments)
    {
        var writer = connection.GetMessageWriter();
        try
        {
            writer.WriteMethodCallHeader(Service, ObjectPath, Interface, member, signature, MessageFlags.None);
            writeArguments?.Invoke(ref writer);
            return writer.CreateMessage();
        }
        finally
        {
            writer.Dispose();
        }
    }

    // Servers announcing "body-markup" parse the body as a small HTML subset.
    private static string EscapeMarkup(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
