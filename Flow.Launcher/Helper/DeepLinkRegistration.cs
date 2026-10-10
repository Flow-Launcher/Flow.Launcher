using System;
using Flow.Launcher.Infrastructure;
using Microsoft.Win32;

namespace Flow.Launcher.Helper;

/// <summary>
/// Registers the .flowplugin file association and the flow-launcher:// URI scheme
/// under HKCU\Software\Classes. Writes are idempotent, so calling on every startup
/// self-heals stale executable paths after updates. No admin rights required.
/// </summary>
public static class DeepLinkRegistration
{
    private static readonly string ClassName = nameof(DeepLinkRegistration);

    private const string ClassesPath = @"Software\Classes";
    private const string ProgId = "Flow.Launcher.PluginPackage";

    private static string OpenCommand => $"\"{Constant.ExecutablePath}\" \"%1\"";
    private static string DefaultIcon => $"\"{Constant.ExecutablePath}\",0";

    public static void EnsureRegistered(bool uriSchemeEnabled)
    {
        try
        {
            RegisterFileExtension();

            if (uriSchemeEnabled)
            {
                RegisterUriScheme();
            }
            else
            {
                UnregisterUriScheme();
            }
        }
        catch (Exception e)
        {
            // Locked-down environments may forbid registry writes; deep links are then unavailable
            App.API.LogError(ClassName, $"Failed to register deep link handlers: {e}");
        }
    }

    private static void RegisterFileExtension()
    {
        using var extensionKey = Registry.CurrentUser.CreateSubKey($@"{ClassesPath}\{DeepLink.PluginFileExtension}");
        extensionKey.SetValue(null, ProgId);

        using var progIdKey = Registry.CurrentUser.CreateSubKey($@"{ClassesPath}\{ProgId}");
        progIdKey.SetValue(null, "Flow Launcher Plugin");

        using var iconKey = progIdKey.CreateSubKey("DefaultIcon");
        iconKey.SetValue(null, DefaultIcon);

        using var commandKey = progIdKey.CreateSubKey(@"shell\open\command");
        commandKey.SetValue(null, OpenCommand);
    }

    public static void RegisterUriScheme()
    {
        using var schemeKey = Registry.CurrentUser.CreateSubKey($@"{ClassesPath}\{DeepLink.Scheme}");
        schemeKey.SetValue(null, "URL:Flow Launcher Protocol");
        schemeKey.SetValue("URL Protocol", "");

        using var iconKey = schemeKey.CreateSubKey("DefaultIcon");
        iconKey.SetValue(null, DefaultIcon);

        using var commandKey = schemeKey.CreateSubKey(@"shell\open\command");
        commandKey.SetValue(null, OpenCommand);
    }

    public static void UnregisterUriScheme()
    {
        Registry.CurrentUser.DeleteSubKeyTree($@"{ClassesPath}\{DeepLink.Scheme}", throwOnMissingSubKey: false);
    }
}
