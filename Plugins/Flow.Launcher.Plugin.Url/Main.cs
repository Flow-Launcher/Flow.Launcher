using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows.Controls;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Plugin.Url
{
    public class Main : IPlugin, IPluginI18n, ISettingProvider
    {
        internal static PluginInitContext Context { get; private set; }
        internal static Settings Settings { get; private set; }

        // Schemes requiring full host validation: domain with TLD, IP address, or localhost
        private static readonly string[] HostValidatedSchemes = ["http", "https"];

        // Chromium browser schemes accepting both :// and : forms (e.g. chrome://settings, chrome:settings)
        private static readonly string[] ChromiumSchemes = ["chrome-extension", "chrome", "brave", "edge", "opera", "vivaldi"];

        // Schemes using :// that are validated by scheme recognition alone — any valid URI structure is accepted
        private static readonly string[] NonHostValidatedDoubleSlashSchemes = [.. ChromiumSchemes, "file", "moz-extension"];

        // Schemes using colon-only syntax (e.g. about:blank, chrome:settings)
        // Chromium schemes also accept the colon form
        private static readonly string[] ColonOnlySchemes = [.. ChromiumSchemes, "about", "data"];

        // All :// schemes
        private static readonly string[] AllDoubleSlashSchemes = [.. HostValidatedSchemes, .. NonHostValidatedDoubleSlashSchemes];

        public List<Result> Query(Query query)
        {
            var raw = query.Search;
            if (!IsURL(raw))
            {
                return [];
            }

            if (IPEndPoint.TryParse(raw, out var endpoint))
            {
                if (endpoint.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && raw[0] != '[' && raw[^1] != ']')
                {
                    // Enclose IPv6 addresses in brackets for URL formatting
                    raw = $"[{raw}]";
                }
            }

            return
                [
                    new()
                    {
                        Title = raw,
                        SubTitle = Localize.flowlauncher_plugin_url_open_url(raw),
                        IcoPath = "Images/url.png",
                        Score = 8,
                        Action = _ =>
                        {
                            // if url was accepted without having any of the recognized scheme, 
                            // then that means no scheme was specified (e.g. www.google.com)
                            // so we add the preferred http/https scheme
                            var hasScheme = (
                                AllDoubleSlashSchemes.Any(scheme => raw.StartsWith(scheme + "://", StringComparison.OrdinalIgnoreCase))
                                ||
                                ColonOnlySchemes.Any(scheme => raw.StartsWith(scheme + ":", StringComparison.OrdinalIgnoreCase))
                            );
                            if (!hasScheme)
                            {
                                raw = GetHttpPreference() + "://" + raw;
                            }
                            try
                            {
                                if (Settings.UseCustomBrowser)
                                {
                                    if (Settings.OpenInNewBrowserWindow)
                                    {
                                        SearchWeb.OpenInBrowserWindow(raw, Settings.BrowserPath, Settings.OpenInPrivateMode, Settings.PrivateModeArgument);
                                    }
                                    else
                                    {
                                        SearchWeb.OpenInBrowserTab(raw, Settings.BrowserPath, Settings.OpenInPrivateMode, Settings.PrivateModeArgument);
                                    }
                                }
                                else
                                {
                                    Context.API.OpenWebUrl(raw);
                                }

                                return true;
                            }
                            catch(Exception)
                            {
                                Context.API.ShowMsgError(Localize.flowlauncher_plugin_url_cannot_open_url(raw));
                                return false;
                            }
                        }
                    }
                ];
        }

        private static string GetHttpPreference()
        {
            return Settings.AlwaysOpenWithHttps ? "https" : "http";
        }

        public bool IsURL(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var input = raw.Trim();

            // Exclude numbers (e.g. 1.2345)
            if (decimal.TryParse(input, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
                return false;

            // Check if it's a bare IP address with optional port, path, query, or fragment
            if (TryMatchBareIP(input, out var bareIPValid))
                return bareIPValid;

            // Check colon-only schemes (e.g. about:blank, chrome:settings)
            if (TryMatchColonScheme(input, out var colonValid))
                return colonValid;

            // Add protocol if missing for Uri validation
            var urlToValidate = AllDoubleSlashSchemes.Any(s => input.StartsWith(s + "://", StringComparison.OrdinalIgnoreCase))
                ? input
                : GetHttpPreference() + "://" + input;

            // At this point it must be a valid absolute URI
            if (!Uri.TryCreate(urlToValidate, UriKind.Absolute, out var uri))
                return false;

            // Other types of supported schemes are handled above so reject if its not a supported :// scheme
            if (!AllDoubleSlashSchemes.Any(scheme => uri.Scheme == scheme))
                return false;

            // Check Non-host-validated :// schemes (e.g. chrome://settings, file:///C:/path)
            if (TryMatchNonHostScheme(uri, input, out var nonHostValid))
                return nonHostValid;

            // Not matched by any other case so treat as a standard host-validated URL
            return ValidateSchemeHost(uri);
        }

        /// <summary>
        /// Checks if input is a bare IP address. 
        /// isValid indicates whether it is a valid/usable address (excludes 0.0.0.0 and ::).
        /// </summary>
        /// <returns>true if input matches IP format, false otherwise</returns>
        private static bool TryMatchBareIP(string input, out bool isValid)
        {
            var ipPart = input.Split('/', '?', '#')[0]; // Remove path, query, and fragment
            if (!IPEndPoint.TryParse(ipPart, out var endpoint))
            {
                isValid = false;
                return false;
            }

            switch (endpoint.AddressFamily)
            {
                case System.Net.Sockets.AddressFamily.InterNetwork:
                    isValid = !endpoint.Address.Equals(IPAddress.Any);
                    return true;
                case System.Net.Sockets.AddressFamily.InterNetworkV6:
                    if (input.Contains('/') || input.Contains('?') || input.Contains('#'))
                    {
                        // Check if IPv6 address is properly bracketed
                        var bracketStart = input.IndexOf('[');
                        var bracketEnd = input.IndexOf(']');
                        if (bracketStart == -1 || bracketEnd == -1 || bracketStart > bracketEnd)
                        {
                            isValid = false;
                            return true;
                        }
                    }
                    isValid = !endpoint.Address.Equals(IPAddress.IPv6Any);
                    return true;
            }

            isValid = true;
            return true;
        }

        /// <summary>
        /// Checks if input matches a colon-only scheme. 
        /// isValid indicates whether the content is non-empty with no whitespace.
        /// </summary>
        /// <returns>true if input matches colon scheme format, false otherwise</returns>
        private static bool TryMatchColonScheme(string input, out bool isValid)
        {
            int colonIndex = input.IndexOf(':');

            bool hasColonPrefix = colonIndex > 0;

            bool isKnownScheme = hasColonPrefix
                && ColonOnlySchemes.Any(s => input[..colonIndex].Equals(s, StringComparison.OrdinalIgnoreCase));
            
            bool isColonOnlySyntax = isKnownScheme 
                && !input[(colonIndex + 1)..].StartsWith("//");

            if (!isColonOnlySyntax)
            {
                isValid = false;
                return false;
            }

            var content = input[(colonIndex + 1)..];
            bool hasContent = content.Length > 0;
            bool hasNoWhitespace = !content.Any(char.IsWhiteSpace);

            isValid = hasContent && hasNoWhitespace;
            return true;
        }

        /// <summary>
        /// Checks if the URI matches a non-host-validated :// scheme. 
        /// isValid indicates whether there is content after the scheme prefix.
        /// </summary>
        /// <returns>true if URI matches a non-host scheme, false otherwise</returns>
        private static bool TryMatchNonHostScheme(Uri uri, string input, out bool isValid)
        {
            if (!NonHostValidatedDoubleSlashSchemes.Any(scheme => uri.Scheme == scheme))
            {
                isValid = false;
                return false;
            }

            string schemePrefix = uri.Scheme + "://";
            bool hasContent = input.Length > schemePrefix.Length;
            isValid = hasContent;
            return true;
        }

        /// <summary>
        /// Validates the host portion of a :// scheme URI.
        /// </summary>
        private static bool ValidateSchemeHost(Uri uri)
        {
            var host = uri.Host;

            // localhost is valid
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                return true;

            // Valid IP address (excluding 0.0.0.0)
            if (IPEndPoint.TryParse(host, out var endpoint))
                return !endpoint.Address.Equals(IPAddress.Any) && !endpoint.Address.Equals(IPAddress.IPv6Any);

            // Domain must have valid format with TLD
            var parts = host.Split('.');
            if (parts.Length < 2 || parts.Any(string.IsNullOrEmpty))
                return false;

            // TLD must be at least 2 characters, allowing letters and digits
            var tld = parts[^1];
            return tld.Length >= 2 && tld.All(char.IsLetterOrDigit);
        }

        public void Init(PluginInitContext context)
        {
            Context = context;

            Settings = context.API.LoadSettingJsonStorage<Settings>();
        }

        public string GetTranslatedPluginTitle()
        {
            return Localize.flowlauncher_plugin_url_plugin_name();
        }

        public string GetTranslatedPluginDescription()
        {
            return Localize.flowlauncher_plugin_url_plugin_description();
        }

        public Control CreateSettingPanel()
        {
            return new SettingsControl();
        }
    }
}
