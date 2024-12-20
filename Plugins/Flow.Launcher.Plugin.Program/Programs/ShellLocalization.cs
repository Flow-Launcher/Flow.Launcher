using System;
using System.IO;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.LibraryLoader;

namespace Flow.Launcher.Plugin.Program.Programs
{
    // From PT Run
    /// <summary>
    /// Class to get localized name of shell items like 'My computer'. The localization is based on the 'windows display language'.
    /// Reused code from https://stackoverflow.com/questions/41423491/how-to-get-localized-name-of-known-folder for the method <see cref="GetLocalizedName"/>
    /// </summary>
    public static class ShellLocalization
    {
        /// <summary>
        /// Returns the localized name of a shell item.
        /// </summary>
        /// <param name="path">Path to the shell item (e. g. shortcut 'File Explorer.lnk').</param>
        /// <returns>The localized name as string or <see cref="string.Empty"/>.</returns>
        public static unsafe string GetLocalizedName(string path)
        {
            int capacity = 1024;
            char[] resourcePathBuffer = new char[capacity];

            // If there is no resource to localize a file name the method returns a non zero value.
            fixed (char* resourcePath = resourcePathBuffer)
            {
                var result = PInvoke.SHGetLocalizedName(path, (PWSTR)resourcePath, (uint)capacity, out var id);
                if (result == HRESULT.S_OK)
                {
                    int validLength = Array.IndexOf(resourcePathBuffer, '\0');
                    if (validLength < 0) validLength = capacity;
                    var resourcePathStr = new string(resourcePathBuffer, 0, validLength);
                    _ = PInvoke.ExpandEnvironmentStrings(resourcePathStr, resourcePath, (uint)capacity);
                    var handle = PInvoke.LoadLibraryEx(resourcePathStr,
                        LOAD_LIBRARY_FLAGS.DONT_RESOLVE_DLL_REFERENCES | LOAD_LIBRARY_FLAGS.LOAD_LIBRARY_AS_DATAFILE);
                    IntPtr safeHandle = handle.DangerousGetHandle();
                    if (safeHandle != IntPtr.Zero)
                    {
                        char[] localizedNameBuffer = new char[capacity];
                        fixed (char* localizedName = localizedNameBuffer)
                        {
                            if (PInvoke.LoadString(handle, (uint)id, (PWSTR)localizedName, capacity) != 0)
                            {
                                validLength = Array.IndexOf(localizedNameBuffer, '\0');
                                if (validLength < 0) validLength = capacity;
                                var lString = new string(localizedNameBuffer, 0, validLength);
                                PInvoke.FreeLibrary(new(safeHandle));
                                return lString;
                            }
                        }

                        PInvoke.FreeLibrary(new(safeHandle));
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// This method returns the localized path to a shell item (folder or file)
        /// </summary>
        /// <param name="path">The path to localize</param>
        /// <returns>The localized path or the original path if localized version is not available</returns>
        public static string GetLocalizedPath(string path)
        {
            path = Environment.ExpandEnvironmentVariables(path);
            string ext = Path.GetExtension(path);
            var pathParts = path.Split("\\");
            string[] locPath = new string[pathParts.Length];

            for (int i = 0; i < pathParts.Length; i++)
            {
                int iElements = i + 1;
                string lName = GetLocalizedName(string.Join("\\", pathParts[..iElements]));
                locPath[i] = !string.IsNullOrEmpty(lName) ? lName : pathParts[i];
            }

            string newPath = string.Join("\\", locPath);
            newPath = !newPath.EndsWith(ext, StringComparison.InvariantCultureIgnoreCase) ? newPath + ext : newPath;

            return newPath;
        }
    }
}
