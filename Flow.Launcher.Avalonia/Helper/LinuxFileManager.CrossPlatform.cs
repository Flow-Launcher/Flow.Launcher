using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Logger;
using Tmds.DBus.Protocol;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// Reveals files in the user's file manager through the org.freedesktop.FileManager1 D-Bus interface (Nautilus,
/// Dolphin, Nemo, Thunar, Caja, PCManFM, ...), the Linux counterpart of Explorer's /select and Finder's open -R.
/// </summary>
internal static class LinuxFileManager
{
    private static readonly string ClassName = nameof(LinuxFileManager);

    /// <summary>
    /// Shows <paramref name="filePath"/> selected in its folder; opens <paramref name="directoryPath"/> with xdg-open
    /// when no file manager implements the interface.
    /// </summary>
    internal static async Task ShowItemOrOpenFolderAsync(string filePath, string directoryPath)
    {
        try
        {
            if (DBusConnection.Session is { } connection)
            {
                await connection.CallMethodAsync(CreateShowItemsMessage(connection, new Uri(filePath).AbsoluteUri))
                    .ConfigureAwait(false);
                return;
            }
        }
        catch (Exception e) when (e is DBusErrorReplyException or DBusConnectionException)
        {
            Log.Info(ClassName, $"FileManager1.ShowItems unavailable ({e.Message}); opening the folder instead");
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = directoryPath, UseShellExecute = true })?.Dispose();
        }
        catch (Exception e)
        {
            Log.Error(ClassName, $"Failed to open folder {directoryPath}: {e.Message}");
        }
    }

    private static MessageBuffer CreateShowItemsMessage(DBusConnection connection, string uri)
    {
        var writer = connection.GetMessageWriter();
        try
        {
            writer.WriteMethodCallHeader("org.freedesktop.FileManager1", "/org/freedesktop/FileManager1",
                "org.freedesktop.FileManager1", "ShowItems", "ass", MessageFlags.None);
            writer.WriteArray(new[] { uri });
            writer.WriteString(string.Empty); // startup notification id
            return writer.CreateMessage();
        }
        finally
        {
            writer.Dispose();
        }
    }
}
