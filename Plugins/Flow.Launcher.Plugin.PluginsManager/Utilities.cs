﻿using Flow.Launcher.Infrastructure.Http;
using ICSharpCode.SharpZipLib.Zip;
using System.IO;
using System.Net;

namespace Flow.Launcher.Plugin.PluginsManager
{
    internal static class Utilities
    {
        /// <summary>
        /// Unzip contents to the given directory.
        /// </summary>
        /// <param name="zipFilePath">The path to the zip file.</param>
        /// <param name="strDirectory">The output directory.</param>
        /// <param name="overwrite">overwrite</param>
        internal static void UnZip(string zipFilePath, string strDirectory, bool overwrite)
        {
            if (strDirectory == "")
                strDirectory = Directory.GetCurrentDirectory();

            using var zipStream = new ZipInputStream(File.OpenRead(zipFilePath));

            ZipEntry theEntry;

            while ((theEntry = zipStream.GetNextEntry()) != null)
            {
                var pathToZip = theEntry.Name;
                var directoryName = string.IsNullOrEmpty(pathToZip) ? "" : Path.GetDirectoryName(pathToZip);
                var fileName = Path.GetFileName(pathToZip);
                var destinationDir = Path.Combine(strDirectory, directoryName);
                var destinationFile = Path.Combine(destinationDir, fileName);

                Directory.CreateDirectory(destinationDir);

                if (string.IsNullOrEmpty(fileName) || (File.Exists(destinationFile) && !overwrite))
                    continue;

                using var streamWriter = File.Create(destinationFile);
                zipStream.CopyTo(streamWriter);
            }
        }

        internal static string GetContainingFolderPathAfterUnzip(string unzippedParentFolderPath)
        {
            var unzippedFolderCount = Directory.GetDirectories(unzippedParentFolderPath).Length;
            var unzippedFilesCount = Directory.GetFiles(unzippedParentFolderPath).Length;

            // adjust path depending on how the plugin is zipped up
            // the recommended should be to zip up the folder not the contents
            if (unzippedFolderCount == 1 && unzippedFilesCount == 0)
                // folder is zipped up, unzipped plugin directory structure: tempPath/unzippedParentPluginFolder/pluginFolderName/
                return Directory.GetDirectories(unzippedParentFolderPath)[0];

            if (unzippedFilesCount > 1)
                // content is zipped up, unzipped plugin directory structure: tempPath/unzippedParentPluginFolder/
               return unzippedParentFolderPath;

            return string.Empty;
        }
    }
}
