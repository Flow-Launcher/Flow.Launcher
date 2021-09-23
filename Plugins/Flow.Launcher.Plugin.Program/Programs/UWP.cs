using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin.Program.Logger;
using Rect = System.Windows.Rect;
using Flow.Launcher.Plugin.SharedModels;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Plugin.Program.Programs
{
    [Serializable]
    public class UWP
    {
        public string Name { get; }
        public string FullName { get; }
        public string FamilyName { get; }
        public string Location { get; set; }

        public Application[] Apps { get; set; }

        public PackageVersion Version { get; set; }

        public UWP(Package package)
        {
            Location = package.InstalledLocation.Path;
            Name = package.Id.Name;
            FullName = package.Id.FullName;
            FamilyName = package.Id.FamilyName;
            InitializeAppInfo();
            Apps = Apps.Where(a =>
            {
                var valid =
                    !string.IsNullOrEmpty(a.UserModelId) &&
                    !string.IsNullOrEmpty(a.DisplayName);
                return valid;
            }).ToArray();
        }

        private void InitializeAppInfo()
        {
            AppxPackageHelper _helper = new AppxPackageHelper();
            var path = Path.Combine(Location, "AppxManifest.xml");

            var namespaces = XmlNamespaces(path);
            InitPackageVersion(namespaces);

            const uint noAttribute = 0x80;
            const Stgm exclusiveRead = Stgm.Read | Stgm.ShareExclusive;
            var hResult = SHCreateStreamOnFileEx(path, exclusiveRead, noAttribute, false, null, out IStream stream);

            if (hResult == Hresult.Ok)
            {
                var apps = new List<Application>();

                List<AppxPackageHelper.IAppxManifestApplication> _apps = _helper.getAppsFromManifest(stream);
                foreach (var _app in _apps)
                {
                    var app = new Application(_app, this);
                    apps.Add(app);
                }

                Apps = apps.Where(a => a.AppListEntry != "none").ToArray();
            }
            else
            {
                var e = Marshal.GetExceptionForHR((int)hResult);
                ProgramLogger.LogException($"|UWP|InitializeAppInfo|{path}" +
                                                "|Error caused while trying to get the details of the UWP program", e);

                Apps = new List<Application>().ToArray();
            }

            if (Marshal.ReleaseComObject(stream) > 0)
            {
                Log.Error("Flow.Launcher.Plugin.Program.Programs.UWP", "AppxManifest.xml was leaked");
            }
        }



        /// http://www.hanselman.com/blog/GetNamespacesFromAnXMLDocumentWithXPathDocumentAndLINQToXML.aspx
        private string[] XmlNamespaces(string path)
        {
            XDocument z = XDocument.Load(path);
            if (z.Root != null)
            {
                var namespaces = z.Root.Attributes().
                    Where(a => a.IsNamespaceDeclaration).
                    GroupBy(
                        a => a.Name.Namespace == XNamespace.None ? string.Empty : a.Name.LocalName,
                        a => XNamespace.Get(a.Value)
                    ).Select(
                        g => g.First().ToString()
                    ).ToArray();
                return namespaces;
            }
            else
            {
                ProgramLogger.LogException($"|UWP|XmlNamespaces|{path}" +
                                                $"|Error occured while trying to get the XML from {path}", new ArgumentNullException());

                return new string[] { };
            }
        }

        private void InitPackageVersion(string[] namespaces)
        {
            var versionFromNamespace = new Dictionary<string, PackageVersion>
            {
                {"http://schemas.microsoft.com/appx/manifest/foundation/windows10", PackageVersion.Windows10},
                {"http://schemas.microsoft.com/appx/2013/manifest", PackageVersion.Windows81},
                {"http://schemas.microsoft.com/appx/2010/manifest", PackageVersion.Windows8},
            };

            foreach (var n in versionFromNamespace.Keys)
            {
                if (namespaces.Contains(n))
                {
                    Version = versionFromNamespace[n];
                    return;
                }
            }

            ProgramLogger.LogException($"|UWP|XmlNamespaces|{Location}" +
                                                "|Trying to get the package version of the UWP program, but a unknown UWP appmanifest version  "
                                                + $"{FullName} from location {Location} is returned.", new FormatException());

            Version = PackageVersion.Unknown;
        }

        public static Application[] All()
        {
            var windows10 = new Version(10, 0);
            var support = Environment.OSVersion.Version.Major >= windows10.Major;
            if (support)
            {
                var applications = CurrentUserPackages().AsParallel().SelectMany(p =>
                {
                    UWP u;
                    try
                    {
                        u = new UWP(p);
                    }
#if !DEBUG
                    catch (Exception e)
                    {
                        ProgramLogger.LogException($"|UWP|All|{p.InstalledLocation}|An unexpected error occured and "
                                                        + $"unable to convert Package to UWP for {p.Id.FullName}", e);
                        return new Application[] { };
                    }
#endif
#if DEBUG //make developer aware and implement handling
                    catch
                    {
                        throw;
                    }
#endif
                    return u.Apps;
                }).ToArray();

                var updatedListWithoutDisabledApps = applications
                                                        .Where(t1 => !Main._settings.DisabledProgramSources
                                                                        .Any(x => x.UniqueIdentifier == t1.UniqueIdentifier))
                                                        .Select(x => x);

                return updatedListWithoutDisabledApps.ToArray();
            }
            else
            {
                return new Application[] { };
            }
        }

        private static IEnumerable<Package> CurrentUserPackages()
        {
            var u = WindowsIdentity.GetCurrent().User;

            if (u != null)
            {
                var id = u.Value;
                PackageManager m;
                try
                {
                    m = new PackageManager();
                }
                catch
                {
                    // Bug from https://github.com/microsoft/CsWinRT, using Microsoft.Windows.SDK.NET.Ref 10.0.19041.0.
                    // Only happens on the first time, so a try catch can fix it.
                    m = new PackageManager();
                }
                var ps = m.FindPackagesForUser(id);
                ps = ps.Where(p =>
                {
                    bool valid;
                    try
                    {
                        var f = p.IsFramework;
                        var d = p.IsDevelopmentMode;
                        var path = p.InstalledLocation.Path;
                        valid = !f && !d && !string.IsNullOrEmpty(path);
                    }
                    catch (Exception e)
                    {
                        ProgramLogger.LogException("UWP", "CurrentUserPackages", $"id", "An unexpected error occured and "
                                                   + $"unable to verify if package is valid", e);
                        return false;
                    }

                    return valid;
                });
                return ps;
            }
            else
            {
                return new Package[] { };
            }
        }

        public override string ToString()
        {
            return FamilyName;
        }

        public override bool Equals(object obj)
        {
            if (obj is UWP uwp)
            {
                return FamilyName.Equals(uwp.FamilyName);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return FamilyName.GetHashCode();
        }

        [Serializable]
        public class Application : IProgram
        {
            public string AppListEntry { get; set; }
            public string UniqueIdentifier { get; set; }
            public string DisplayName { get; set; }
            public string Description { get; set; }
            public string UserModelId { get; set; }
            public string BackgroundColor { get; set; }

            public string EntryPoint { get; set; }
            public string Name => DisplayName;
            public string Location => Package.Location;

            public bool Enabled { get; set; }
            public bool CanRunElevated { get; set; }

            public string LogoUri { get; set; }
            public string LogoPath { get; set; }
            public UWP Package { get; set; }

            public Application() { }


            public Result Result(string query, IPublicAPI api)
            {
                string title;
                MatchResult matchResult;

                // We suppose Name won't be null
                if (!Main._settings.EnableDescription || Description == null || Name.StartsWith(Description))
                {
                    title = Name;
                    matchResult = StringMatcher.FuzzySearch(query, title);
                }
                else if (Description.StartsWith(Name))
                {
                    title = Description;
                    matchResult = StringMatcher.FuzzySearch(query, Description);
                }
                else
                {
                    title = $"{Name}: {Description}";
                    var nameMatch = StringMatcher.FuzzySearch(query, Name);
                    var desciptionMatch = StringMatcher.FuzzySearch(query, Description);
                    if (desciptionMatch.Score > nameMatch.Score)
                    {
                        for (int i = 0; i < desciptionMatch.MatchData.Count; i++)
                        {
                            desciptionMatch.MatchData[i] += Name.Length + 2; // 2 is ": "
                        }
                        matchResult = desciptionMatch;
                    }
                    else matchResult = nameMatch;
                }

                if (!matchResult.Success)
                    return null;

                var result = new Result
                {
                    Title = title,
                    SubTitle = Package.Location,
                    Icon = Logo,
                    Score = matchResult.Score,
                    TitleHighlightData = matchResult.MatchData,
                    ContextData = this,
                    Action = e =>
                    {
                        var elevated = (
                            e.SpecialKeyState.CtrlPressed &&
                            e.SpecialKeyState.ShiftPressed &&
                            !e.SpecialKeyState.AltPressed &&
                            !e.SpecialKeyState.WinPressed
                        );

                        if (elevated && CanRunElevated)
                        {
                            LaunchElevated();
                        }
                        else
                        {
                            Launch(api);

                            if (elevated)
                            {
                                var title = "Plugin: Program";
                                var message = api.GetTranslation("flowlauncher_plugin_program_run_as_administrator_not_supported_message");
                                api.ShowMsg(title, message, string.Empty);
                            }
                        }

                        return true;
                    }
                };


                return result;
            }

            public List<Result> ContextMenus(IPublicAPI api)
            {
                var contextMenus = new List<Result>
                {
                    new Result
                    {
                        Title = api.GetTranslation("flowlauncher_plugin_program_open_containing_folder"),

                        Action = _ =>
                        {
                            Main.StartProcess(Process.Start,
                                                new ProcessStartInfo(
                                                    !string.IsNullOrEmpty(Main._settings.CustomizedExplorer)
                                                    ? Main._settings.CustomizedExplorer
                                                    : Settings.Explorer,
                                                    Main._settings.CustomizedArgs
                                                        .Replace("%s",$"\"{Package.Location}\"")
                                                        .Trim()));

                            return true;
                        },

                        IcoPath = "Images/folder.png"
                    }
                };

                if (CanRunElevated)
                {
                    contextMenus.Add(new Result
                    {
                        Title = api.GetTranslation("flowlauncher_plugin_program_run_as_administrator"),
                        Action = _ =>
                        {
                            LaunchElevated();
                            return true;
                        },
                        IcoPath = "Images/cmd.png"
                    });
                }

                return contextMenus;
            }

            private async void Launch(IPublicAPI api)
            {
                var appManager = new ApplicationActivationHelper.ApplicationActivationManager();
                const string noArgs = "";
                const ApplicationActivationHelper.ActivateOptions noFlags = ApplicationActivationHelper.ActivateOptions.None;
                await Task.Run(() =>
                {
                    try
                    {
                        appManager.ActivateApplication(UserModelId, noArgs, noFlags, out _);
                    }
                    catch (Exception)
                    {
                        var name = "Plugin: Program";
                        var message = $"Can't start UWP: {DisplayName}";
                        api.ShowMsg(name, message, string.Empty);
                    }
                });
            }

            private async void LaunchElevated()
            {
                string command = "shell:AppsFolder\\" + UniqueIdentifier;
                command = Environment.ExpandEnvironmentVariables(command.Trim());

                var info = new ProcessStartInfo(command)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                };

                Main.StartProcess(Process.Start, info);
            }

            public Application(AppxPackageHelper.IAppxManifestApplication manifestApp, UWP package)
            {
                // This is done because we cannot use the keyword 'out' along with a property

                manifestApp.GetAppUserModelId(out string tmpUserModelId);
                manifestApp.GetAppUserModelId(out string tmpUniqueIdentifier);
                manifestApp.GetStringValue("DisplayName", out string tmpDisplayName);
                manifestApp.GetStringValue("Description", out string tmpDescription);
                manifestApp.GetStringValue("BackgroundColor", out string tmpBackgroundColor);
                manifestApp.GetStringValue("EntryPoint", out string tmpEntryPoint);

                UserModelId = tmpUserModelId;
                UniqueIdentifier = tmpUniqueIdentifier;
                DisplayName = tmpDisplayName;
                Description = tmpDescription;
                BackgroundColor = tmpBackgroundColor;
                EntryPoint = tmpEntryPoint;

                Package = package;

                DisplayName = ResourceFromPri(package.FullName, package.Name, DisplayName);
                Description = ResourceFromPri(package.FullName, package.Name, Description);
                LogoUri = LogoUriFromManifest(manifestApp);
                LogoPath = LogoPathFromUri(LogoUri);

                Enabled = true;
                CanRunElevated = CanApplicationRunElevated();
            }

            private bool CanApplicationRunElevated()
            {
                if (EntryPoint == "Windows.FullTrustApplication")
                {
                    return true;
                }

                var manifest = Package.Location + "\\AppxManifest.xml";
                if (File.Exists(manifest))
                {
                    var file = File.ReadAllText(manifest);

                    if (file.Contains("TrustLevel=\"mediumIL\"", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            internal string ResourceFromPri(string packageFullName, string packageName, string rawReferenceValue)
            {
                if (string.IsNullOrWhiteSpace(rawReferenceValue) || !rawReferenceValue.StartsWith("ms-resource:"))
                    return rawReferenceValue;

                var formattedPriReference = FormattedPriReferenceValue(packageName, rawReferenceValue);

                var outBuffer = new StringBuilder(128);
                string source = $"@{{{packageFullName}? {formattedPriReference}}}";
                var capacity = (uint)outBuffer.Capacity;
                var hResult = SHLoadIndirectString(source, outBuffer, capacity, IntPtr.Zero);
                if (hResult == Hresult.Ok)
                {
                    var loaded = outBuffer.ToString();
                    if (!string.IsNullOrEmpty(loaded))
                    {
                        return loaded;
                    }
                    else
                    {
                        ProgramLogger.LogException($"|UWP|ResourceFromPri|{Package.Location}|Can't load null or empty result "
                                                    + $"pri {source} in uwp location {Package.Location}", new NullReferenceException());
                        return string.Empty;
                    }
                }
                else
                {
                    var e = Marshal.GetExceptionForHR((int)hResult);
                    ProgramLogger.LogException($"|UWP|ResourceFromPri|{Package.Location}|Load pri failed {source} with HResult {hResult} and location {Package.Location}", e);
                    return string.Empty;
                }
            }

            public string FormattedPriReferenceValue(string packageName, string rawPriReferenceValue)
            {
                const string prefix = "ms-resource:";

                if (string.IsNullOrWhiteSpace(rawPriReferenceValue) || !rawPriReferenceValue.StartsWith(prefix))
                    return rawPriReferenceValue;

                string key = rawPriReferenceValue.Substring(prefix.Length);
                if (key.StartsWith("//"))
                    return $"{prefix}{key}";

                if (!key.StartsWith("/"))
                {
                    key = $"/{key}";
                }

                if (!key.ToLower().Contains("resources"))
                {
                    key = $"/Resources{key}";
                }

                return $"{prefix}//{packageName}{key}";
            }

            internal string LogoUriFromManifest(AppxPackageHelper.IAppxManifestApplication app)
            {
                var logoKeyFromVersion = new Dictionary<PackageVersion, string>
                {
                    { PackageVersion.Windows10, "Square44x44Logo" },
                    { PackageVersion.Windows81, "Square30x30Logo" },
                    { PackageVersion.Windows8, "SmallLogo" },
                };
                if (logoKeyFromVersion.ContainsKey(Package.Version))
                {
                    var key = logoKeyFromVersion[Package.Version];
                    app.GetStringValue(key, out string logoUri);
                    return logoUri;
                }
                else
                {
                    return string.Empty;
                }
            }

            internal string LogoPathFromUri(string uri)
            {
                // all https://msdn.microsoft.com/windows/uwp/controls-and-patterns/tiles-and-notifications-app-assets
                // windows 10 https://msdn.microsoft.com/en-us/library/windows/apps/dn934817.aspx
                // windows 8.1 https://msdn.microsoft.com/en-us/library/windows/apps/hh965372.aspx#target_size
                // windows 8 https://msdn.microsoft.com/en-us/library/windows/apps/br211475.aspx

                string path;
                if (uri.Contains("\\"))
                {
                    path = Path.Combine(Package.Location, uri);
                }
                else
                {
                    // for C:\Windows\MiracastView etc
                    path = Path.Combine(Package.Location, "Assets", uri);
                }

                var extension = Path.GetExtension(path);
                if (extension != null)
                {
                    var end = path.Length - extension.Length;
                    var prefix = path.Substring(0, end);
                    var paths = new List<string> { path };

                    var scaleFactors = new Dictionary<PackageVersion, List<int>>
                    {
                        // scale factors on win10: https://docs.microsoft.com/en-us/windows/uwp/controls-and-patterns/tiles-and-notifications-app-assets#asset-size-tables,
                        { PackageVersion.Windows10, new List<int> { 100, 125, 150, 200, 400 } },
                        { PackageVersion.Windows81, new List<int> { 100, 120, 140, 160, 180 } },
                        { PackageVersion.Windows8, new List<int> { 100 } }
                    };

                    if (scaleFactors.ContainsKey(Package.Version))
                    {
                        foreach (var factor in scaleFactors[Package.Version])
                        {
                            paths.Add($"{prefix}.scale-{factor}{extension}");
                        }
                    }

                    var selected = paths.FirstOrDefault(File.Exists);
                    if (!string.IsNullOrEmpty(selected))
                    {
                        return selected;
                    }
                    else
                    {
                        ProgramLogger.LogException($"|UWP|LogoPathFromUri|{Package.Location}" +
                                                    $"|{UserModelId} can't find logo uri for {uri} in package location: {Package.Location}", new FileNotFoundException());
                        return string.Empty;
                    }
                }
                else
                {
                    ProgramLogger.LogException($"|UWP|LogoPathFromUri|{Package.Location}" +
                                                    $"|Unable to find extension from {uri} for {UserModelId} " +
                                                    $"in package location {Package.Location}", new FileNotFoundException());
                    return string.Empty;
                }
            }


            public ImageSource Logo()
            {
                var logo = ImageFromPath(LogoPath);
                var plated = PlatedImage(logo);

                // todo magic! temp fix for cross thread object
                plated.Freeze();
                return plated;
            }


            private BitmapImage ImageFromPath(string path)
            {
                if (File.Exists(path))
                {
                    var image = new BitmapImage(new Uri(path));
                    return image;
                }
                else
                {
                    ProgramLogger.LogException($"|UWP|ImageFromPath|{(string.IsNullOrEmpty(path) ? "Not Avaliable" : path)}" +
                                                    $"|Unable to get logo for {UserModelId} from {path} and" +
                                                    $" located in {Package.Location}", new FileNotFoundException());
                    return new BitmapImage(new Uri(Constant.MissingImgIcon));
                }
            }

            private ImageSource PlatedImage(BitmapImage image)
            {
                if (!string.IsNullOrEmpty(BackgroundColor) && BackgroundColor != "transparent")
                {
                    var width = image.Width;
                    var height = image.Height;
                    var x = 0;
                    var y = 0;

                    var group = new DrawingGroup();

                    var converted = ColorConverter.ConvertFromString(BackgroundColor);
                    if (converted != null)
                    {
                        var color = (Color)converted;
                        var brush = new SolidColorBrush(color);
                        var pen = new Pen(brush, 1);
                        var backgroundArea = new Rect(0, 0, width, width);
                        var rectabgle = new RectangleGeometry(backgroundArea);
                        var rectDrawing = new GeometryDrawing(brush, pen, rectabgle);
                        group.Children.Add(rectDrawing);

                        var imageArea = new Rect(x, y, image.Width, image.Height);
                        var imageDrawing = new ImageDrawing(image, imageArea);
                        group.Children.Add(imageDrawing);

                        // http://stackoverflow.com/questions/6676072/get-system-drawing-bitmap-of-a-wpf-area-using-visualbrush
                        var visual = new DrawingVisual();
                        var context = visual.RenderOpen();
                        context.DrawDrawing(group);
                        context.Close();
                        const int dpiScale100 = 96;
                        var bitmap = new RenderTargetBitmap(
                            Convert.ToInt32(width), Convert.ToInt32(height),
                            dpiScale100, dpiScale100,
                            PixelFormats.Pbgra32
                        );
                        bitmap.Render(visual);
                        return bitmap;
                    }
                    else
                    {
                        ProgramLogger.LogException($"|UWP|PlatedImage|{Package.Location}" +
                                                    $"|Unable to convert background string {BackgroundColor} " +
                                                    $"to color for {Package.Location}", new InvalidOperationException());

                        return new BitmapImage(new Uri(Constant.MissingImgIcon));
                    }
                }
                else
                {
                    // todo use windows theme as background
                    return image;
                }
            }

            public override string ToString()
            {
                return $"{DisplayName}: {Description}";
            }
        }

        public enum PackageVersion
        {
            Windows10,
            Windows81,
            Windows8,
            Unknown
        }

        [Flags]
        private enum Stgm : uint
        {
            Read = 0x0,
            ShareExclusive = 0x10,
        }

        private enum Hresult : uint
        {
            Ok = 0x0000,
        }

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern Hresult SHCreateStreamOnFileEx(string fileName, Stgm grfMode, uint attributes, bool create,
            IStream reserved, out IStream stream);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern Hresult SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, uint cchOutBuf,
            IntPtr ppvReserved);
    }
}