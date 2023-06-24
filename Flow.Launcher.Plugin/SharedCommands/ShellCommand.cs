using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Flow.Launcher.Plugin.SharedCommands
{
    public static class ShellCommand
    {
        public delegate bool EnumThreadDelegate(IntPtr hwnd, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint threadId, EnumThreadDelegate lpfn, IntPtr lParam);
        [DllImport("user32.dll")] static extern int GetWindowText(IntPtr hwnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr hwnd);

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
                EnumThreadWindows((uint)ptc[i].Id, CheckSecurityThread, IntPtr.Zero);
        }

        private static bool CheckSecurityThread(IntPtr hwnd, IntPtr lParam)
        {
            if (GetWindowTitle(hwnd) == "Windows Security")
                containsSecurityWindow = true;
            return true;
        }

        private static string GetWindowTitle(IntPtr hwnd)
        {
            StringBuilder sb = new StringBuilder(GetWindowTextLength(hwnd) + 1);
            GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static ProcessStartInfo SetProcessStartInfo(this string fileName, string workingDirectory = "", string arguments = "", string verb = "", bool createNoWindow = false)
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
