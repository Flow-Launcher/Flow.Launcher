using System;
using System.Collections.Specialized;
using System.IO;
using System.Web;

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

    /// <summary>
    /// Normalizes command line arguments to a single deep link URI string, or null for a normal launch.
    /// Raw flow-launcher:// arguments are dropped when <paramref name="allowSchemeArgs"/> is false
    /// (URI scheme disabled in settings); --query and .flowplugin paths are always honored.
    /// </summary>
    public static string FromCommandLineArgs(string[] args, bool allowSchemeArgs)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--query" || args[i] == "-query") && i + 1 < args.Length)
            {
                return $"{SchemePrefix}query?q={Uri.EscapeDataString(args[i + 1])}";
            }

            if (args[i].StartsWith(SchemePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return allowSchemeArgs ? args[i] : null;
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
}
