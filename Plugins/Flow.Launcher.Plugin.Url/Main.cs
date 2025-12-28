using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Plugin.Url
{
    public class Main : IPlugin, IPluginI18n, ISettingProvider
    {
        //based on https://gist.github.com/dperini/729294
        private const string UrlPattern = "^" +
            // protocol identifier
            "(?:(?:https?|ftp)://|)" +
            // user:pass authentication
            "(?:\\S+(?::\\S*)?@)?" +
            "(?:" +
            // IPv6 address with optional brackets (brackets required if followed by port)
            // IPv6 with brackets - patterns ordered from most specific to most general
            "(?:\\[(?:[0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}\\]|" + // Full IPv6 (8 groups)
            "\\[(?:[0-9a-fA-F]{1,4}:){6}:[0-9a-fA-F]{1,4}\\]|" + // 6 groups :: 1 group
            "\\[(?:[0-9a-fA-F]{1,4}:){5}:(?:[0-9a-fA-F]{1,4}:)?[0-9a-fA-F]{1,4}\\]|" + // 5 groups :: 1-2 groups
            "\\[(?:[0-9a-fA-F]{1,4}:){4}:(?:[0-9a-fA-F]{1,4}:){0,2}[0-9a-fA-F]{1,4}\\]|" + // 4 groups :: 1-3 groups
            "\\[(?:[0-9a-fA-F]{1,4}:){3}:(?:[0-9a-fA-F]{1,4}:){0,3}[0-9a-fA-F]{1,4}\\]|" + // 3 groups :: 1-4 groups
            "\\[(?:[0-9a-fA-F]{1,4}:){2}:(?:[0-9a-fA-F]{1,4}:){0,4}[0-9a-fA-F]{1,4}\\]|" + // 2 groups :: 1-5 groups
            "\\[[0-9a-fA-F]{1,4}::(?:[0-9a-fA-F]{1,4}:){0,5}[0-9a-fA-F]{1,4}\\]|" + // 1 group :: 1-6 groups
            "\\[::(?:[0-9a-fA-F]{1,4}:){0,6}[0-9a-fA-F]{1,4}\\]|" + // :: followed by 1-7 groups
            "\\[::\\]|" + // All zeros
            "\\[(?:[0-9a-fA-F]{1,4}:){1,7}:\\])" + // 1-7 groups ::
            "|" +
            // IPv6 without brackets (only when no port follows)
            // Patterns ordered from most specific to most general to avoid incorrect partial matches
            "(?:(?:[0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}|" + // Full IPv6 (8 groups)
            "(?:[0-9a-fA-F]{1,4}:){6}:[0-9a-fA-F]{1,4}|" + // 6 groups :: 1 group
            "(?:[0-9a-fA-F]{1,4}:){5}:(?:[0-9a-fA-F]{1,4}:)?[0-9a-fA-F]{1,4}|" + // 5 groups :: 1-2 groups
            "(?:[0-9a-fA-F]{1,4}:){4}:(?:[0-9a-fA-F]{1,4}:){0,2}[0-9a-fA-F]{1,4}|" + // 4 groups :: 1-3 groups
            "(?:[0-9a-fA-F]{1,4}:){3}:(?:[0-9a-fA-F]{1,4}:){0,3}[0-9a-fA-F]{1,4}|" + // 3 groups :: 1-4 groups
            "(?:[0-9a-fA-F]{1,4}:){2}:(?:[0-9a-fA-F]{1,4}:){0,4}[0-9a-fA-F]{1,4}|" + // 2 groups :: 1-5 groups
            "[0-9a-fA-F]{1,4}::(?:[0-9a-fA-F]{1,4}:){0,5}[0-9a-fA-F]{1,4}|" + // 1 group :: 1-6 groups
            "::(?:[0-9a-fA-F]{1,4}:){0,6}[0-9a-fA-F]{1,4}|" + // :: followed by 1-7 groups
            "::|" + // All zeros
            "(?:[0-9a-fA-F]{1,4}:){1,7}:)(?!:[0-9])" + // 1-7 groups :: (not followed by port)
            "|" +
            // IPv4 address - all valid addresses including private networks (excluding 0.0.0.0)
            "(?:(?:25[0-5]|2[0-4]\\d|1\\d\\d|[1-9]\\d|[1-9])\\.(?:25[0-5]|2[0-4]\\d|1\\d\\d|[1-9]?\\d)\\.(?:25[0-5]|2[0-4]\\d|1\\d\\d|[1-9]?\\d)\\.(?:25[0-5]|2[0-4]\\d|1\\d\\d|[1-9]?\\d))" +
            "|" +
            // localhost
            "localhost" +
            "|" +
            // host name
            "(?:(?:[a-z\\u00a1-\\uffff0-9]-*)*[a-z\\u00a1-\\uffff0-9]+)" +
            // domain name
            "(?:\\.(?:[a-z\\u00a1-\\uffff0-9]-*)*[a-z\\u00a1-\\uffff0-9]+)*" +
            // TLD identifier
            "(?:\\.(?:[a-z\\u00a1-\\uffff]{2,}))" +
            ")" +
            // port number
            "(?::\\d{1,5})?" +
            // resource path
            "(?:/\\S*)?" +
            "$";
        private readonly Regex UrlRegex = new(UrlPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
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
            raw = raw.ToLower();

            if (UrlRegex.Match(raw).Value == raw) return true;

            return false;
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
