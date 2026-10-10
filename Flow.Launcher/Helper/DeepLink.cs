using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Helper;

/// <summary>
/// Normalizes command line arguments into flow-launcher:// deep link URIs
/// and routes them to the matching verb handler.
/// </summary>
public static class DeepLink
{
    public const string Scheme = "flow-launcher";
    public const string SchemePrefix = Scheme + "://";
    public const string PluginFileExtension = ".flowplugin";

    private static readonly string ClassName = nameof(DeepLink);

    private static readonly Dictionary<string, Action<NameValueCollection>> Handlers = new()
    {
        ["query"] = HandleQuery,
        ["settings"] = HandleSettings,
        ["plugin/install"] = HandlePluginInstall,
    };

    /// <summary>
    /// Normalizes command line arguments to a single deep link URI string, or null for a normal launch.
    /// Raw flow-launcher:// arguments are dropped when <paramref name="allowSchemeArgs"/> is false
    /// (URI scheme disabled in settings); --query/-q and .flowplugin paths are always honored.
    /// </summary>
    public static string FromCommandLineArgs(string[] args, bool allowSchemeArgs)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--query" || args[i] == "-q")
            {
                if (i + 1 >= args.Length || string.IsNullOrEmpty(args[i + 1]))
                {
                    return null;
                }

                return $"{SchemePrefix}query?q={Uri.EscapeDataString(args[i + 1])}";
            }

            if (args[i].StartsWith(SchemePrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (!allowSchemeArgs)
                {
                    // This runs in Main before App/App.API exist, so we use the static infrastructure
                    // logger directly (same pattern as ErrorReporting.cs) instead of App.API.LogWarn.
                    // Only the fact of the drop is logged at Warn; the full arg may carry user data.
                    Log.Warn(ClassName, "Dropped a raw deep link arg because the URI scheme protocol is disabled in settings.");
                    Log.Debug(ClassName, $"Dropped raw deep link arg <{args[i]}>.");
                    return null;
                }

                return args[i];
            }

            if (args[i].EndsWith(PluginFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return $"{SchemePrefix}plugin/install?path={Uri.EscapeDataString(Path.GetFullPath(args[i]))}";
            }
        }

        return null;
    }

    /// <summary>
    /// Parses a deep link URI into a lowercase verb (host plus optional path, e.g. "plugin/install")
    /// and its query parameters. Returns false for anything that is not a flow-launcher:// URI.
    /// </summary>
    public static bool TryParse(string payload, out string verb, out NameValueCollection parameters)
    {
        verb = null;
        parameters = null;

        if (string.IsNullOrEmpty(payload) || !Uri.TryCreate(payload, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        verb = uri.Host.ToLowerInvariant();
        var subPath = uri.AbsolutePath.Trim('/');
        if (!string.IsNullOrEmpty(subPath))
        {
            verb = $"{verb}/{subPath.ToLowerInvariant()}";
        }

        parameters = HttpUtility.ParseQueryString(uri.Query);
        return true;
    }

    /// <summary>
    /// Routes a deep link payload to its verb handler. Payloads that are not
    /// flow-launcher:// URIs are treated as plain query text for backward compatibility
    /// with older second instances that sent the raw --query value.
    /// </summary>
    public static void Dispatch(string payload)
    {
        if (string.IsNullOrEmpty(payload)) return;

        if (!TryParse(payload, out var verb, out var parameters))
        {
            ChangeQueryAndShow(payload);
            return;
        }

        if (Handlers.TryGetValue(verb, out var handler))
        {
            handler(parameters);
        }
        else
        {
            // Only the verb is logged at Warn; the full payload may carry user query text, paths, or URLs
            App.API.LogWarn(ClassName, $"Unrecognized deep link verb <{verb}>");
            App.API.LogDebug(ClassName, $"Unrecognized deep link payload <{payload}>");
            App.API.ShowMsgError(Localize.deepLinkUnrecognizedTitle(), Localize.deepLinkUnrecognizedSubtitle(payload));
        }
    }

    private static void HandleQuery(NameValueCollection parameters)
    {
        ChangeQueryAndShow(parameters["q"]);
    }

    private static void ChangeQueryAndShow(string query)
    {
        App.API.ShowMainWindow();
        if (string.IsNullOrEmpty(query)) return;

        // Make sure to go back to the query results page first since it can cause issues if current page is context menu
        App.API.BackToQueryResults();
        App.API.ChangeQuery(query, true);
    }

    private static void HandleSettings(NameValueCollection parameters)
    {
        App.API.OpenSettingDialog();
    }

    private static void HandlePluginInstall(NameValueCollection parameters)
    {
        var path = parameters["path"];
        var id = parameters["id"];
        var url = parameters["url"];

        if (!HasExactlyOneInstallIdentifier(path, id, url))
        {
            App.API.ShowMsgError(Localize.deepLinkInstallInvalidTitle(), Localize.deepLinkInstallInvalidSubtitle());
            return;
        }

        if (!string.IsNullOrEmpty(path))
        {
            if (!File.Exists(path))
            {
                App.API.ShowMsgError(Localize.deepLinkInstallInvalidTitle(), Localize.deepLinkInstallFileNotFound(path));
                return;
            }

            _ = PluginInstaller.InstallPluginAndCheckRestartAsync(path);
        }
        else if (!string.IsNullOrEmpty(id))
        {
            _ = InstallByIdAsync(id);
        }
        else
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var downloadUri) || downloadUri.Scheme != Uri.UriSchemeHttps)
            {
                App.API.ShowMsgError(Localize.deepLinkInstallInvalidTitle(), Localize.deepLinkInstallHttpsOnly());
                return;
            }

            _ = PluginInstaller.InstallPluginFromWebAndCheckRestartAsync(url);
        }
    }

    /// <summary>
    /// A plugin install deep link must carry exactly one identifier — a local file path, a manifest
    /// id, or a download url. Zero identifiers leaves nothing to install; more than one is ambiguous.
    /// </summary>
    public static bool HasExactlyOneInstallIdentifier(string path, string id, string url)
    {
        return new[] { path, id, url }.Count(x => !string.IsNullOrEmpty(x)) == 1;
    }

    private static async Task InstallByIdAsync(string id)
    {
        var manifestUpdated = await App.API.UpdatePluginManifestAsync();
        if (!manifestUpdated && App.API.GetPluginManifest().Count == 0)
        {
            App.API.ShowMsgError(Localize.deepLinkInstallInvalidTitle(), Localize.deepLinkManifestUpdateFailed());
            return;
        }

        var plugin = App.API.GetPluginManifest()
            .FirstOrDefault(x => string.Equals(x.ID, id, StringComparison.OrdinalIgnoreCase));
        if (plugin == null)
        {
            App.API.ShowMsgError(Localize.deepLinkInstallInvalidTitle(), Localize.deepLinkInstallPluginNotFound(id));
            return;
        }

        await PluginInstaller.InstallPluginAndCheckRestartAsync(plugin);
    }
}
