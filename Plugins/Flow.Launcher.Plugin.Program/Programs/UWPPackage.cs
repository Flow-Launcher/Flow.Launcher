using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin.Program.Logger;
using Flow.Launcher.Plugin.SharedModels;
using System.Threading.Channels;
using System.Xml;
using Windows.ApplicationModel.Core;
using System.Windows.Input;
using MemoryPack;

namespace Flow.Launcher.Plugin.Program.Programs
{
    [MemoryPackable]
    public partial class UWPPackage
    {
        public string Name { get; }
        public string FullName { get; }
        public string FamilyName { get; }
        public string Location { get; set; }

        public UWPApp[] Apps { get; set; } = Array.Empty<UWPApp>();


        /// <summary>
        /// For serialization
        /// </summary>
        [MemoryPackConstructor]
        private UWPPackage()
        {
        }

        public UWPPackage(Package package)
        {
            Location = package.InstalledLocation.Path;
            Name = package.Id.Name;
            FullName = package.Id.FullName;
            FamilyName = package.Id.FamilyName;
        }

        public void InitAppsInPackage(Package package)
        {
            var apps = new List<UWPApp>();
            // WinRT
            var appListEntries = package.GetAppListEntries();
            foreach (var app in appListEntries)
            {
                try
                {
                    var tmp = new UWPApp(app, this);
                    apps.Add(tmp);
                }
                catch (Exception e)
                {
                    ProgramLogger.LogException($"|UWP|InitAppsInPackage|{Location}" +
                                               "|Unexpected exception occurs when trying to construct a Application from package"
                                               + $"{FullName} from location {Location}", e);
                }
            }

            Apps = apps.ToArray();

            try
            {
                var xmlDoc = GetManifestXml();
                if (xmlDoc == null)
                {
                    return;
                }

                var xmlRoot = xmlDoc.DocumentElement;
                var packageVersion = GetPackageVersionFromManifest(xmlRoot);
                if (!smallLogoNameFromVersion.TryGetValue(packageVersion, out string logoName) ||
                    !bigLogoNameFromVersion.TryGetValue(packageVersion, out string bigLogoName))
                {
                    return;
                }

                var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
                namespaceManager.AddNamespace("d",
                    "http://schemas.microsoft.com/appx/manifest/foundation/windows10"); // still need a name
                namespaceManager.AddNamespace("rescap",
                    "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities");
                namespaceManager.AddNamespace("uap10", "http://schemas.microsoft.com/appx/manifest/uap/windows10/10");

                var allowElevationNode =
                    xmlRoot.SelectSingleNode("//rescap:Capability[@Name='allowElevation']", namespaceManager);
                bool packageCanElevate = allowElevationNode != null;

                var appsNode = xmlRoot.SelectSingleNode("d:Applications", namespaceManager);
                foreach (var app in Apps)
                {
                    // According to https://learn.microsoft.com/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps#create-a-package-manifest-for-the-sparse-package
                    // and https://learn.microsoft.com/uwp/schemas/appxpackage/uapmanifestschema/element-application#attributes
                    var id = app.UserModelId.Split('!')[1];
                    var appNode = appsNode?.SelectSingleNode($"d:Application[@Id='{id}']", namespaceManager);
                    if (appNode != null)
                    {
                        app.CanRunElevated = packageCanElevate || UWPApp.IfAppCanRunElevated(appNode);

                        // local name to fit all versions
                        var visualElement =
                            appNode.SelectSingleNode($"*[local-name()='VisualElements']", namespaceManager);
                        var logoUri = visualElement?.Attributes[logoName]?.Value;
                        app.LogoPath = app.LogoPathFromUri(logoUri, (64, 64));
                        // use small logo or may have a big margin
                        var previewUri = visualElement?.Attributes[logoName]?.Value;
                        app.PreviewImagePath = app.LogoPathFromUri(previewUri, (256, 256));
                    }
                }
            }
            catch (Exception e)
            {
                ProgramLogger.LogException($"|UWP|InitAppsInPackage|{Location}" +
                                           "|Unexpected exception occurs when trying to construct a Application from package"
                                           + $"{FullName} from location {Location}", e);
            }
        }

        private XmlDocument GetManifestXml()
        {
            var manifest = Path.Combine(Location, "AppxManifest.xml");
            try
            {
                var file = File.ReadAllText(manifest);
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(file);
                return xmlDoc;
            }
            catch (FileNotFoundException e)
            {
                ProgramLogger.LogException("UWP", "GetManifestXml", $"{Location}", "AppxManifest.xml not found.", e);
                return null;
            }
            catch (Exception e)
            {
                ProgramLogger.LogException("UWP", "GetManifestXml", $"{Location}",
                    "An unexpected error occurred and unable to parse AppxManifest.xml", e);
                return null;
            }
        }

        private PackageVersion GetPackageVersionFromManifest(XmlNode xmlRoot)
        {
            if (xmlRoot != null)
            {
                var namespaces = xmlRoot.Attributes;
                foreach (XmlAttribute ns in namespaces)
                {
                    if (versionFromNamespace.TryGetValue(ns.Value, out var packageVersion))
                    {
                        return packageVersion;
                    }
                }

                ProgramLogger.LogException($"|UWP|GetPackageVersionFromManifest|{Location}" +
                                           "|Trying to get the package version of the UWP program, but an unknown UWP app-manifest version in package "
                                           + $"{FullName} from location {Location}", new FormatException());
                return PackageVersion.Unknown;
            }
            else
            {
                ProgramLogger.LogException($"|UWP|GetPackageVersionFromManifest|{Location}" +
                                           "|Can't parse AppManifest.xml of package "
                                           + $"{FullName} from location {Location}",
                    new ArgumentNullException(nameof(xmlRoot)));
                return PackageVersion.Unknown;
            }
        }

        private static readonly Dictionary<string, PackageVersion> versionFromNamespace = new()
        {
            { "http://schemas.microsoft.com/appx/manifest/foundation/windows10", PackageVersion.Windows10 },
            { "http://schemas.microsoft.com/appx/2013/manifest", PackageVersion.Windows81 },
            { "http://schemas.microsoft.com/appx/2010/manifest", PackageVersion.Windows8 },
        };

        private static readonly Dictionary<PackageVersion, string> smallLogoNameFromVersion = new()
        {
            { PackageVersion.Windows10, "Square44x44Logo" },
            { PackageVersion.Windows81, "Square30x30Logo" },
            { PackageVersion.Windows8, "SmallLogo" },
        };

        private static readonly Dictionary<PackageVersion, string> bigLogoNameFromVersion = new()
        {
            { PackageVersion.Windows10, "Square150x150Logo" },
            { PackageVersion.Windows81, "Square150x150Logo" },
            { PackageVersion.Windows8, "Logo" },
        };

        public static UWPApp[] All(Settings settings)
        {
            var support = SupportUWP();
            if (support && settings.EnableUWP)
            {
                var applications = CurrentUserPackages().AsParallel().SelectMany(p =>
                {
                    UWPPackage u;
                    try
                    {
                        u = new UWPPackage(p);
                        u.InitAppsInPackage(p);
                    }
#if !DEBUG
                    catch (Exception e)
                    {
                        ProgramLogger.LogException($"|UWP|All|{p.InstalledLocation}|An unexpected error occurred and unable to convert Package to UWP for {p.Id.FullName}", e);
                        return Array.Empty<UWPApp>();
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
                        .Any(x => x.UniqueIdentifier == t1.UniqueIdentifier));

                return updatedListWithoutDisabledApps.ToArray();
            }
            else
            {
                return Array.Empty<UWPApp>();
            }
        }

        public static bool SupportUWP()
        {
            var windows10 = new Version(10, 0);
            var support = Environment.OSVersion.Version.Major >= windows10.Major;
            return support;
        }

        private static IEnumerable<Package> CurrentUserPackages()
        {
            var user = WindowsIdentity.GetCurrent().User;

            if (user != null)
            {
                var userId = user.Value;
                PackageManager packageManager;
                try
                {
                    packageManager = new PackageManager();
                }
                catch
                {
                    // Bug from https://github.com/microsoft/CsWinRT, using Microsoft.Windows.SDK.NET.Ref 10.0.19041.0.
                    // Only happens on the first time, so a try catch can fix it.
                    packageManager = new PackageManager();
                }

                var packages = packageManager.FindPackagesForUser(userId);
                packages = packages.Where(p =>
                {
                    try
                    {
                        var f = p.IsFramework;
                        var d = p.IsDevelopmentMode;
                        var path = p.InstalledLocation.Path;
                        return !f && !d && !string.IsNullOrEmpty(path);
                    }
                    catch (Exception e)
                    {
                        ProgramLogger.LogException("UWP", "CurrentUserPackages", $"{p.Id.FullName}",
                            "An unexpected error occurred and "
                            + $"unable to verify if package is valid", e);
                        return false;
                    }
                });
                return packages;
            }
            else
            {
                return Array.Empty<Package>();
            }
        }

        private static Channel<byte> PackageChangeChannel = Channel.CreateBounded<byte>(1);

        public static async Task WatchPackageChange()
        {
            if (Environment.OSVersion.Version.Major >= 10)
            {
                var catalog = PackageCatalog.OpenForCurrentUser();
                catalog.PackageInstalling += (_, args) =>
                {
                    if (args.IsComplete)
                        PackageChangeChannel.Writer.TryWrite(default);
                };
                catalog.PackageUninstalling += (_, args) =>
                {
                    if (args.IsComplete)
                        PackageChangeChannel.Writer.TryWrite(default);
                };
                catalog.PackageUpdating += (_, args) =>
                {
                    if (args.IsComplete)
                        PackageChangeChannel.Writer.TryWrite(default);
                };

                while (await PackageChangeChannel.Reader.WaitToReadAsync().ConfigureAwait(false))
                {
                    await Task.Delay(3000).ConfigureAwait(false);
                    PackageChangeChannel.Reader.TryRead(out _);
                    await Task.Run(Main.IndexUwpPrograms);
                }
            }
        }

        public override string ToString()
        {
            return FamilyName;
        }

        public override bool Equals(object obj)
        {
            if (obj is UWPPackage uwp)
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


        public enum PackageVersion
        {
            Windows10,
            Windows81,
            Windows8,
            Unknown
        }
    }

    [MemoryPackable]
    public partial class UWPApp : IProgram
    {
        private string _uid = string.Empty;

        public string UniqueIdentifier
        {
            get => _uid;
            set => _uid = value == null ? string.Empty : value.ToLowerInvariant();
        }

        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public string UserModelId { get; set; } = string.Empty;

        //public string BackgroundColor { get; set; } = string.Empty; // preserve for future use
        public string Name => DisplayName;
        public string Location { get; set; } = string.Empty;

        public bool Enabled { get; set; } = false;
        public bool CanRunElevated { get; set; } = false;
        public string LogoPath { get; set; } = string.Empty;
        public string PreviewImagePath { get; set; } = string.Empty;

        [MemoryPackConstructor]
        private UWPApp()
        {
        }

        public UWPApp(AppListEntry appListEntry, UWPPackage package)
        {
            UserModelId = appListEntry.AppUserModelId;
            UniqueIdentifier = appListEntry.AppUserModelId;
            DisplayName = appListEntry.DisplayInfo.DisplayName;
            Description = appListEntry.DisplayInfo.Description;
            Location = package.Location;
            Enabled = true;
        }

        public Result Result(string query, IPublicAPI api)
        {
            string title;
            MatchResult matchResult;

            // We suppose Name won't be null
            if (!Main._settings.EnableDescription || string.IsNullOrWhiteSpace(Description) || Name.Equals(Description))
            {
                title = Name;
                matchResult = StringMatcher.FuzzySearch(query, Name);
            }
            else
            {
                title = $"{Name}: {Description}";
                var nameMatch = StringMatcher.FuzzySearch(query, Name);
                var descriptionMatch = StringMatcher.FuzzySearch(query, Description);
                if (descriptionMatch.Score > nameMatch.Score)
                {
                    for (int i = 0; i < descriptionMatch.MatchData.Count; i++)
                    {
                        descriptionMatch.MatchData[i] += Name.Length + 2; // 2 is ": "
                    }

                    matchResult = descriptionMatch;
                }
                else
                {
                    matchResult = nameMatch;
                }
            }

            if (!matchResult.IsSearchPrecisionScoreMet())
                return null;

            var result = new Result
            {
                Title = title,
                AutoCompleteText = Name,
                SubTitle = Main._settings.HideAppsPath ? string.Empty : Location,
                IcoPath = LogoPath,
                Preview = new Result.PreviewInfo
                {
                    IsMedia = false, PreviewImagePath = PreviewImagePath, Description = Description
                },
                Score = matchResult.Score,
                TitleHighlightData = matchResult.MatchData,
                ContextData = this,
                Action = e =>
                {
                    // Ctrl + Enter to open containing folder
                    bool openFolder = e.SpecialKeyState.ToModifierKeys() == ModifierKeys.Control;
                    if (openFolder)
                    {
                        Main.Context.API.OpenDirectory(Location);
                        return true;
                    }

                    // Ctrl + Shift + Enter to run elevated
                    bool elevated = e.SpecialKeyState.ToModifierKeys() == (ModifierKeys.Control | ModifierKeys.Shift);

                    bool shouldRunElevated = elevated && CanRunElevated;
                    _ = Task.Run(() => Launch(shouldRunElevated)).ConfigureAwait(false);
                    if (elevated && !shouldRunElevated)
                    {
                        var title = api.GetTranslation("flowlauncher_plugin_program_disable_dlgtitle_error");
                        var message =
                            api.GetTranslation(
                                "flowlauncher_plugin_program_run_as_administrator_not_supported_message");
                        api.ShowMsg(title, message, string.Empty);
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
                        Main.Context.API.OpenDirectory(Location);

                        return true;
                    },
                    IcoPath = "Images/folder.png",
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe838"),
                }
            };

            if (CanRunElevated)
            {
                contextMenus.Add(new Result
                {
                    Title = api.GetTranslation("flowlauncher_plugin_program_run_as_administrator"),
                    Action = _ =>
                    {
                        Task.Run(() => Launch(true)).ConfigureAwait(false);
                        return true;
                    },
                    IcoPath = "Images/cmd.png",
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe7ef")
                });
            }

            return contextMenus;
        }

        private void Launch(bool elevated = false)
        {
            string command = "shell:AppsFolder\\" + UserModelId;
            command = Environment.ExpandEnvironmentVariables(command.Trim());

            var info = new ProcessStartInfo(command) { UseShellExecute = true, Verb = elevated ? "runas" : "" };

            Main.StartProcess(Process.Start, info);
        }

        internal static bool IfAppCanRunElevated(XmlNode appNode)
        {
            // According to https://learn.microsoft.com/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps#create-a-package-manifest-for-the-sparse-package
            // and https://learn.microsoft.com/uwp/schemas/appxpackage/uapmanifestschema/element-application#attributes

            return appNode?.Attributes["EntryPoint"]?.Value == "Windows.FullTrustApplication" ||
                   appNode?.Attributes["uap10:TrustLevel"]?.Value == "mediumIL";
        }

        internal string LogoPathFromUri(string uri, (int, int) desiredSize)
        {
            // all https://msdn.microsoft.com/windows/uwp/controls-and-patterns/tiles-and-notifications-app-assets
            // windows 10 https://msdn.microsoft.com/en-us/library/windows/apps/dn934817.aspx
            // windows 8.1 https://msdn.microsoft.com/en-us/library/windows/apps/hh965372.aspx#target_size
            // windows 8 https://msdn.microsoft.com/en-us/library/windows/apps/br211475.aspx

            if (string.IsNullOrWhiteSpace(uri))
            {
                ProgramLogger.LogException($"|UWP|LogoPathFromUri|{Location}" +
                                           $"|{UserModelId} 's logo uri is null or empty: {Location}",
                    new ArgumentException("uri"));
                return string.Empty;
            }

            string path = Path.Combine(Location, uri);

            var pxCount = desiredSize.Item1 * desiredSize.Item2;
            var logoPath = TryToFindLogo(uri, path, pxCount);
            if (logoPath == string.Empty)
            {
                var tmp = Path.Combine(Location, "Assets", uri);
                if (!path.Equals(tmp, StringComparison.OrdinalIgnoreCase))
                {
                    // TODO: Don't know why, just keep it at the moment
                    // Maybe on older version of Windows 10?
                    // for C:\Windows\MiracastView etc
                    return TryToFindLogo(uri, tmp, pxCount);
                }
            }

            return logoPath;

            string TryToFindLogo(string uri, string path, int px)
            {
                var extension = Path.GetExtension(path);
                if (extension != null)
                {
                    //if (File.Exists(path))
                    //{
                    //    return path; // shortcut, avoid enumerating files
                    //}

                    var logoNamePrefix = Path.GetFileNameWithoutExtension(uri); // e.g Square44x44
                    var logoDir = Path.GetDirectoryName(path); // e.g ..\..\Assets
                    if (String.IsNullOrEmpty(logoNamePrefix) || !Directory.Exists(logoDir))
                    {
                        // Known issue: Edge always triggers it since logo is not at uri
                        ProgramLogger.LogException($"|UWP|LogoPathFromUri|{Location}" +
                                                   $"|{UserModelId} can't find logo uri for {uri} in package location (logo name or directory not found): {Location}",
                            new FileNotFoundException());
                        return string.Empty;
                    }

                    var logos = Directory.EnumerateFiles(logoDir, $"{logoNamePrefix}*{extension}");

                    // Currently we don't care which one to choose
                    // Just ignore all qualifiers
                    // select like logo.[xxx_yyy].png
                    // https://learn.microsoft.com/en-us/windows/uwp/app-resources/tailor-resources-lang-scale-contrast

                    // todo select from file name like pt run
                    var selected = logos.FirstOrDefault();
                    var closest = selected;
                    int min = int.MaxValue;
                    foreach (var logo in logos)
                    {
                        var imageStream = File.OpenRead(logo);
                        var decoder = BitmapDecoder.Create(imageStream, BitmapCreateOptions.IgnoreColorProfile,
                            BitmapCacheOption.None);
                        var height = decoder.Frames[0].PixelHeight;
                        var width = decoder.Frames[0].PixelWidth;
                        int pixelCountDiff = Math.Abs(height * width - px);
                        if (pixelCountDiff < min)
                        {
                            // try to find the closest to desired size
                            closest = logo;
                            if (pixelCountDiff == 0)
                                break; // found
                            min = pixelCountDiff;
                        }
                    }

                    selected = closest;
                    if (!string.IsNullOrEmpty(selected))
                    {
                        return selected;
                    }
                    else
                    {
                        ProgramLogger.LogException($"|UWP|LogoPathFromUri|{Location}" +
                                                   $"|{UserModelId} can't find logo uri for {uri} in package location (can't find specified logo): {Location}",
                            new FileNotFoundException());
                        return string.Empty;
                    }
                }
                else
                {
                    ProgramLogger.LogException($"|UWP|LogoPathFromUri|{Location}" +
                                               $"|Unable to find extension from {uri} for {UserModelId} " +
                                               $"in package location {Location}", new FileNotFoundException());
                    return string.Empty;
                }
            }
        }


        #region logo legacy

        // preserve for potential future use

        //public ImageSource Logo()
        //{
        //    var logo = ImageFromPath(LogoPath);
        //    var plated = PlatedImage(logo);  // TODO: maybe get plated directly from app package?

        //    // todo magic! temp fix for cross thread object
        //    plated.Freeze();
        //    return plated;
        //}
        //private BitmapImage ImageFromPath(string path)
        //{
        //    if (File.Exists(path))
        //    {
        //        var image = new BitmapImage();
        //        image.BeginInit();
        //        image.UriSource = new Uri(path);
        //        image.CacheOption = BitmapCacheOption.OnLoad;
        //        image.EndInit();
        //        image.Freeze();
        //        return image;
        //    }
        //    else
        //    {
        //        ProgramLogger.LogException($"|UWP|ImageFromPath|{(string.IsNullOrEmpty(path) ? "Not Available" : path)}" +
        //                                   $"|Unable to get logo for {UserModelId} from {path} and" +
        //                                   $" located in {Location}", new FileNotFoundException());
        //        return new BitmapImage(new Uri(Constant.MissingImgIcon));
        //    }
        //}

        //private ImageSource PlatedImage(BitmapImage image)
        //{
        //    if (!string.IsNullOrEmpty(BackgroundColor) && BackgroundColor != "transparent")
        //    {
        //        var width = image.Width;
        //        var height = image.Height;
        //        var x = 0;
        //        var y = 0;

        //        var group = new DrawingGroup();

        //        var converted = ColorConverter.ConvertFromString(BackgroundColor);
        //        if (converted != null)
        //        {
        //            var color = (Color)converted;
        //            var brush = new SolidColorBrush(color);
        //            var pen = new Pen(brush, 1);
        //            var backgroundArea = new Rect(0, 0, width, width);
        //            var rectangle = new RectangleGeometry(backgroundArea);
        //            var rectDrawing = new GeometryDrawing(brush, pen, rectangle);
        //            group.Children.Add(rectDrawing);

        //            var imageArea = new Rect(x, y, image.Width, image.Height);
        //            var imageDrawing = new ImageDrawing(image, imageArea);
        //            group.Children.Add(imageDrawing);

        //            // http://stackoverflow.com/questions/6676072/get-system-drawing-bitmap-of-a-wpf-area-using-visualbrush
        //            var visual = new DrawingVisual();
        //            var context = visual.RenderOpen();
        //            context.DrawDrawing(group);
        //            context.Close();
        //            const int dpiScale100 = 96;
        //            var bitmap = new RenderTargetBitmap(
        //                Convert.ToInt32(width), Convert.ToInt32(height),
        //                dpiScale100, dpiScale100,
        //                PixelFormats.Pbgra32
        //            );
        //            bitmap.Render(visual);
        //            return bitmap;
        //        }
        //        else
        //        {
        //            ProgramLogger.LogException($"|UWP|PlatedImage|{Location}" +
        //                                       $"|Unable to convert background string {BackgroundColor} " +
        //                                       $"to color for {Location}", new InvalidOperationException());

        //            return new BitmapImage(new Uri(Constant.MissingImgIcon));
        //        }
        //    }
        //    else
        //    {
        //        // todo use windows theme as background
        //        return image;
        //    }
        //}

        #endregion

        public override string ToString()
        {
            return $"{DisplayName}: {Description}";
        }

        public override bool Equals(object obj)
        {
            if (obj is UWPApp other)
            {
                return UniqueIdentifier == other.UniqueIdentifier;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return UniqueIdentifier.GetHashCode();
        }
    }
}
