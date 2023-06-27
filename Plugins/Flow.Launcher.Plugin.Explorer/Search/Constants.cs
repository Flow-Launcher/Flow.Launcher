using System.IO;
using System.Reflection;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    internal static class Constants
    {
        internal const string FolderImagePath = "Images\\folder.png";
        internal const string FileImagePath = "Images\\file.png";
        internal const string DeleteFileFolderImagePath = "Images\\deletefilefolder.png";
        internal const string CopyImagePath = "Images\\copy.png";
        internal const string IndexImagePath = "Images\\index.png";
        internal const string ExcludeFromIndexImagePath = "Images\\excludeindexpath.png";
        internal const string ExplorerIconImagePath = "Images\\explorer.png";
        internal const string DifferentUserIconImagePath = "Images\\user.png";
        internal const string IndexingOptionsIconImagePath = "Images\\windowsindexingoptions.png";
        internal const string QuickAccessImagePath = "Images\\quickaccess.png";
        internal const string RemoveQuickAccessImagePath = "Images\\removequickaccess.png";
        internal const string ShowContextMenuImagePath = "Images\\context_menu.png";
        internal const string EverythingErrorImagePath = "Images\\everything_error.png";
        internal const string IndexSearchWarningImagePath = "Images\\index_error.png";
        internal const string WindowsIndexErrorImagePath = "Images\\index_error2.png";
        internal const string GeneralSearchErrorImagePath = "Images\\robot_error.png";


        internal const string ToolTipOpenDirectory = "Ctrl + Enter to open the directory";

        internal const string ToolTipOpenContainingFolder = "Ctrl + Enter to open the containing folder";

        internal const char AllFilesFolderSearchWildcard = '>';

        internal const string DefaultContentSearchActionKeyword = "doc:";

        internal const char DirectorySeparator = '\\';

        internal const string WindowsIndexingOptions = "srchadmin.dll";

        internal static string ExplorerIconImageFullPath 
            => Directory.GetParent(Assembly.GetExecutingAssembly().Location.ToString()) + "\\" + ExplorerIconImagePath;
    }
}
