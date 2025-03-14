using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Flow.Launcher.Plugin.SharedCommands
{
    public static class ShellCommand
    {
        public delegate bool EnumThreadDelegate(IntPtr hwnd, IntPtr lParam);

        private static bool containsSecurityWindow;

        public static Process RunAsDifferentUser(ProcessStartInfo processStartInfo)
        {
            processStartInfo.Verb = "RunAsUser";
            var process = Process.Start(processStartInfo);

            containsSecurityWindow = false;
            while (!containsSecurityWindow) // wait for windows to bring up the "Windows Security" dialog
            {
                CheckSecurityWindow();
                Thread.Sleep(25);
            }

            while (containsSecurityWindow) // while this process contains a "Windows Security" dialog, stay open
            {
                containsSecurityWindow = false;
                CheckSecurityWindow();
                Thread.Sleep(50);
            }

            return process;
        }

        private static void CheckSecurityWindow()
        {
            ProcessThreadCollection ptc = Process.GetCurrentProcess().Threads;
            for (int i = 0; i < ptc.Count; i++)
                PInvoke.EnumThreadWindows((uint)ptc[i].Id, CheckSecurityThread, IntPtr.Zero);
        }

        private static BOOL CheckSecurityThread(HWND hwnd, LPARAM lParam)
        {
            if (GetWindowTitle(hwnd) == "Windows Security")
                containsSecurityWindow = true;
            return true;
        }

        private static unsafe string GetWindowTitle(HWND hwnd)
        {
            var capacity = PInvoke.GetWindowTextLength(hwnd) + 1;
            int length;
            Span<char> buffer = capacity < 1024 ? stackalloc char[capacity] : new char[capacity];
            fixed (char* pBuffer = buffer)
            {
                // If the window has no title bar or text, if the title bar is empty,
                // or if the window or control handle is invalid, the return value is zero.
                length = PInvoke.GetWindowText(hwnd, pBuffer, capacity);
            }

            return buffer[..length].ToString();
        }

        public static ProcessStartInfo SetProcessStartInfo(this string fileName, string workingDirectory = "",
            string arguments = "", string verb = "", bool createNoWindow = false)
        {
            var info = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                Arguments = arguments,
                Verb = verb,
                CreateNoWindow = createNoWindow
            };

            return info;
        }

        /// <summary>
        /// Runs a windows command using the provided ProcessStartInfo
        /// </summary>
        /// <exception cref="FileNotFoundException">Thrown when unable to find the file specified in the command </exception>
        /// <exception cref="Win32Exception">Thrown when error occurs during the execution of the command </exception>
        public static void Execute(ProcessStartInfo info)
        {
            Execute(Process.Start, info);
        }

        /// <summary>
        /// Runs a windows command using the provided ProcessStartInfo using a custom execute command function
        /// </summary>
        /// <param name="startProcess">allows you to pass in a custom command execution function</param>
        /// <param name="info">allows you to pass in the info that will be passed to startProcess</param>
        /// <exception cref="FileNotFoundException">Thrown when unable to find the file specified in the command </exception>
        /// <exception cref="Win32Exception">Thrown when error occurs during the execution of the command </exception>
        public static void Execute(Func<ProcessStartInfo, Process> startProcess, ProcessStartInfo info)
        {
            startProcess(info);
        }
    }
}
