using System;
using System.Collections.Generic;
using System.Text;

namespace Flow.Launcher.Plugin.Explorer.Search
{
    public static class Constants
    {
        public const string FolderImagePath = "Images\\folder.png";
        public const string FileImagePath = "Images\\file.png";
        public const string DeleteFileFolderImagePath = "Images\\deletefilefolder.png";
        public const string CopyImagePath = "Images\\copy.png";
        public const string IndexImagePath = "Images\\index.png";

        internal static readonly char[] SpecialSearchChars = new char[]
        {
            '?', '*', '>'
        };
    }
}
