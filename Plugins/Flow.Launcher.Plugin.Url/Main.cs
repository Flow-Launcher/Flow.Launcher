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

        private static readonly string[] UrlSchemes = ["http://", "https://", "ftp://"];

        public List<Result> Query(Query query)
        {
            var raw = query.Search;
            if (!IsURL(raw))
            {
                return [];
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
                            // not a recognized scheme, add preferred http scheme
                            if (!UrlSchemes.Any(scheme => raw.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)))
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
            var ipPart = input.Split('/', '?', '#')[0]; // Remove path, query, and fragment
            if (IPEndPoint.TryParse(ipPart, out var endpoint) && !endpoint.Address.Equals(IPAddress.Any) && !endpoint.Address.Equals(IPAddress.IPv6Any))
                return true;

            // Add protocol if missing for Uri validation
            var urlToValidate = UrlSchemes.Any(s => input.StartsWith(s, StringComparison.OrdinalIgnoreCase))
                ? input
                : GetHttpPreference() + "://" + input;

            if (!Uri.TryCreate(urlToValidate, UriKind.Absolute, out var uri))
                return false;

            // Validate protocol
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeFtp)
                return false;

            var host = uri.Host;

            // localhost is valid
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                return true;

            // Valid IP address (excluding 0.0.0.0)
            if (IPAddress.TryParse(host, out var hostIp))
                return !hostIp.Equals(IPAddress.Any) && !hostIp.Equals(IPAddress.IPv6Any);

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
