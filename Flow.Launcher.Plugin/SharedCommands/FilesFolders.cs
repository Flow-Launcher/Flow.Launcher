using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;

namespace Flow.Launcher.Plugin.SharedCommands
{
    public static class FilesFolders
    {
        private const string FileExplorerProgramName = "explorer";

        private const string FileExplorerProgramEXE = "explorer.exe";

        /// <summary>
        /// Copies the folder and all of its files and folders 
        /// including subfolders to the target location
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="targetPath"></param>
        public static void CopyAll(this string sourcePath, string targetPath)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourcePath);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourcePath);
            }

            try
            {
                DirectoryInfo[] dirs = dir.GetDirectories();
                // If the destination directory doesn't exist, create it.
                if (!Directory.Exists(targetPath))
                {
                    Directory.CreateDirectory(targetPath);
                }

                // Get the files in the directory and copy them to the new location.
                FileInfo[] files = dir.GetFiles();
                foreach (FileInfo file in files)
                {
                    string temppath = Path.Combine(targetPath, file.Name);
                    file.CopyTo(temppath, false);
                }

                // Recursively copy subdirectories by calling itself on each subdirectory until there are no more to copy
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(targetPath, subdir.Name);
                    CopyAll(subdir.FullName, temppath);
                }
            }
            catch (Exception e)
            {
#if DEBUG
                throw e;
#else
                MessageBox.Show(string.Format("Copying path {0} has failed, it will now be deleted for consistency", targetPath));
                RemoveFolderIfExists(targetPath);
#endif
            }

        }

        public static bool VerifyBothFolderFilesEqual(this string fromPath, string toPath)
        {
            try
            {
                var fromDir = new DirectoryInfo(fromPath);
                var toDir = new DirectoryInfo(toPath);

                if (fromDir.GetFiles("*", SearchOption.AllDirectories).Length != toDir.GetFiles("*", SearchOption.AllDirectories).Length)
                    return false;

                if (fromDir.GetDirectories("*", SearchOption.AllDirectories).Length != toDir.GetDirectories("*", SearchOption.AllDirectories).Length)
                    return false;

                return true;
            }
            catch (Exception e)
            {
#if DEBUG
                throw e;
#else
                MessageBox.Show(string.Format("Unable to verify folders and files between {0} and {1}", fromPath, toPath));
                return false;
#endif
            }

        }

        public static void RemoveFolderIfExists(this string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch (Exception e)
            {
#if DEBUG
                throw e;
#else
                MessageBox.Show(string.Format("Not able to delete folder {0}, please go to the location and manually delete it", path));
#endif
            }
        }

        public static bool LocationExists(this string path)
        {
            return Directory.Exists(path);
        }

        public static bool FileExists(this string filePath)
        {
            return File.Exists(filePath);
        }

        public static void OpenPath(string fileOrFolderPath)
        {
            var psi = new ProcessStartInfo { FileName = FileExplorerProgramName, UseShellExecute = true, Arguments = '"' + fileOrFolderPath + '"' };
            try
            {
                if (LocationExists(fileOrFolderPath) || FileExists(fileOrFolderPath))
                    Process.Start(psi);
            }
            catch (Exception e)
            {
#if DEBUG
                throw e;
#else
                MessageBox.Show(string.Format("Unable to open the path {0}, please check if it exists", fileOrFolderPath));
#endif
            }
        }

        public static void OpenContainingFolder(string path)
        {
            Process.Start(FileExplorerProgramEXE, $" /select,\"{path}\"");
        }

        ///<summary>
        /// This checks whether a given string is a directory path or network location string. 
        /// It does not check if location actually exists.
        ///</summary>
        public static bool IsLocationPathString(this string querySearchString)
        {
            if (string.IsNullOrEmpty(querySearchString) || querySearchString.Length < 3)
                return false;

            // // shared folder location, and not \\\location\
            if (querySearchString.StartsWith(@"\\")
                && querySearchString[2] != '\\')
                return true;

            // c:\
            if (char.IsLetter(querySearchString[0])
                && querySearchString[1] == ':'
                && querySearchString[2] == '\\')
            {
                return querySearchString.Length == 3 || querySearchString[3] != '\\';
            }

            return false;
        }

        ///<summary>
        /// Gets the previous level directory from a path string.
        /// Checks that previous level directory exists and returns it 
        /// as a path string, or empty string if doesn't exist
        ///</summary>
        public static string GetPreviousExistingDirectory(Func<string, bool> locationExists, string path)
        {
            var previousDirectoryPath = "";
            var index = path.LastIndexOf('\\');
            if (index > 0 && index < (path.Length - 1))
            {
                previousDirectoryPath = path.Substring(0, index + 1);
                if (!locationExists(previousDirectoryPath))
                {
                    return "";
                }
            }
            else
            {
                return "";
            }

            return previousDirectoryPath;
        }

        ///<summary>
        /// Returns a dictionary list of sub-directories and files for a given path
        ///</summary>
        public static List<Dictionary<string, string>> DirectoryRecursiveSearch(
            string searchPath,
            EnumerationOptions enumerationOption,
            string searchCriteria, CancellationToken token)
        {
            var results = new List<Dictionary<string, string>>();

            var path = ReturnPreviousDirectoryIfIncompleteString(searchPath);

            var directoryInfo = new DirectoryInfo(path);

            foreach (var fileSystemInfo in directoryInfo.EnumerateFileSystemInfos(searchCriteria, enumerationOption))
            {
                if (fileSystemInfo is DirectoryInfo)
                {
                    results.Add(
                    new Dictionary<string, string>()
                    {
                            { "Type", "Folder" },
                            { "Name", fileSystemInfo.Name },
                            { "Path", fileSystemInfo.FullName }
                    });
                }
                else
                {
                    results.Add(
                    new Dictionary<string, string>()
                    {
                            { "Type", "File" },
                            { "Name", fileSystemInfo.Name },
                            { "Path", fileSystemInfo.FullName }
                    });
                }

                token.ThrowIfCancellationRequested();
            }

            return results;
        }

        ///<summary>
        /// Returns the previous level directory if path incomplete (does not end with '\').
        /// Does not check if previous level directory exists.
        /// Returns passed in string if is complete path
        ///</summary>
        public static string ReturnPreviousDirectoryIfIncompleteString(string path)
        {
            if (!path.EndsWith("\\"))
            {
                // not full path, get previous level directory string
                var indexOfSeparator = path.LastIndexOf('\\');

                return path.Substring(0, indexOfSeparator + 1);
            }

            return path;
        }
    }
}
