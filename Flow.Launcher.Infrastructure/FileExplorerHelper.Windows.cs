using System;

namespace Flow.Launcher.Infrastructure
{
    public static class FileExplorerHelper
    {
        /// <summary>
        /// Gets the path of the file explorer that is currently in the foreground
        /// </summary>
        public static string GetActiveExplorerPath()
        {
            var explorerPath = DialogJump.DialogJump.GetActiveExplorerPath();
            return !string.IsNullOrEmpty(explorerPath) ?
                GetDirectoryPath(new Uri(explorerPath).LocalPath) :
                null;
        }

        /// <summary>
        /// Get directory path from a file path
        /// </summary>
        private static string GetDirectoryPath(string path)
        {
            if (!path.EndsWith('\\'))
            {
                return path + "\\";
            }

            return path;
        }
    }
}
