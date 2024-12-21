#nullable enable

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections.Generic;
using win32api = Microsoft.Win32;

namespace Flow.Launcher.Infrastructure
{
    public static class Helper
    {
        static Helper()
        {
            jsonFormattedSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        }

        /// <summary>
        /// http://www.yinwang.org/blog-cn/2015/11/21/programming-philosophy
        /// </summary>
        public static T NonNull<T>(this T? obj)
        {
            if (obj == null)
            {
                throw new NullReferenceException();
            }
            else
            {
                return obj;
            }
        }

        public static void RequireNonNull<T>(this T obj)
        {
            if (obj == null)
            {
                throw new NullReferenceException();
            }
        }

        public static void ValidateDataDirectory(string bundledDataDirectory, string dataDirectory)
        {
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            foreach (var bundledDataPath in Directory.GetFiles(bundledDataDirectory))
            {
                var data = Path.GetFileName(bundledDataPath);
                var dataPath = Path.Combine(dataDirectory, data.NonNull());
                if (!File.Exists(dataPath))
                {
                    File.Copy(bundledDataPath, dataPath);
                }
                else
                {
                    var time1 = new FileInfo(bundledDataPath).LastWriteTimeUtc;
                    var time2 = new FileInfo(dataPath).LastWriteTimeUtc;
                    if (time1 != time2)
                    {
                        File.Copy(bundledDataPath, dataPath, true);
                    }
                }
            }
        }

        public static void ValidateDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private static readonly JsonSerializerOptions jsonFormattedSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public static string Formatted<T>(this T t)
        {
            var formatted = JsonSerializer.Serialize(t, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            return formatted;
        }

        public static string GetActiveOfficeFilePath()
        {
            var pid = GetActiveWindowProcessId();
            var handle = win32api.OpenProcess(win32api.PROCESS_QUERY_INFORMATION | win32api.PROCESS_VM_READ, false, pid);
            var exePath = win32api.GetModuleFileNameEx(handle, 0);
            if (exePath.ToLower().Contains("winword.exe"))
            {
                return Path.GetFullPath(new win32api.Dispatch("Word.Application").ActiveDocument.FullName);
            }
            else if (exePath.ToLower().Contains("powerpnt.exe"))
            {
                return Path.GetFullPath(new win32api.Dispatch("PowerPoint.Application").ActivePresentation.FullName);
            }
            else if (exePath.ToLower().Contains("excel.exe"))
            {
                return Path.GetFullPath(new win32api.Dispatch("Excel.Application").ActiveWorkbook.FullName);
            }
            else
            {
                return null;
            }
        }

        private static int GetActiveWindowProcessId()
        {
            var window = GetForegroundWindow();
            var threadProcessId = GetWindowThreadProcessId(window, out var processId);
            return processId;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
    }
}
