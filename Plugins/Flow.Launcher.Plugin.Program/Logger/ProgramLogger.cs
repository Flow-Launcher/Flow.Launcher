using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Plugin.Program.Logger
{
    /// <summary>
    /// The Program plugin has seen many issues recorded in the Flow Launcher repo related to various loading of Windows programs.
    /// This is a dedicated logger for this Program plugin with the aim to output a more friendlier message and clearer
    /// log that will allow debugging to be quicker and easier.
    /// </summary>
    internal static class ProgramLogger
    {
        public const string DirectoryName = "Logs";

        /// <summary>
        /// Logs an exception
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        internal static void LogException(string classname, string callingMethodName, string loadingProgramPath,
            string interpretationMessage, Exception e)
        {
            var logger = LogManager.GetLogger("");

            var innerExceptionNumber = 1;

            var possibleResolution = "Not yet known";
            var errorStatus = "UNKNOWN";

            logger.Error("------------- BEGIN Flow.Launcher.Plugin.Program exception -------------");

            do
            {
                if (IsKnownWinProgramError(e, callingMethodName) || IsKnownUWPProgramError(e, callingMethodName))
                {
                    possibleResolution = "Can be ignored and Flow Launcher should still continue, however the program may not be loaded";
                    errorStatus = "KNOWN";
                }

                var calledMethod = e.TargetSite != null ? e.TargetSite.ToString() : e.StackTrace;

                calledMethod = string.IsNullOrEmpty(calledMethod) ? "Not available" : calledMethod;

                logger.Error($"\nException full name: {e.GetType().FullName}"
                             + $"\nError status: {errorStatus}"
                             + $"\nClass name: {classname}"
                             + $"\nCalling method: {callingMethodName}"
                             + $"\nProgram path: {loadingProgramPath}"
                             + $"\nInnerException number: {innerExceptionNumber}"
                             + $"\nException message: {e.Message}"
                             + $"\nException error type: HResult {e.HResult}"
                             + $"\nException thrown in called method: {calledMethod}"
                             + $"\nPossible interpretation of the error: {interpretationMessage}"
                             + $"\nPossible resolution: {possibleResolution}");

                innerExceptionNumber++;
                e = e.InnerException;
            } while (e != null);

            logger.Error("------------- END Flow.Launcher.Plugin.Program exception -------------");
        }

        /// <summary>
        /// Please follow exception format: |class name|calling method name|loading program path|user friendly message that explains the error
        /// => Example: |Win32|LnkProgram|c:\..\chrome.exe|Permission denied on directory, but Flow Launcher should continue
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        internal static void LogException(string message, Exception e)
        {
            //Index 0 is always empty.
            var parts = message.Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
            {
                var logger = LogManager.GetLogger("");
                logger.Error(e, $"fail to log exception in program logger, parts length is too small: {parts.Length}, message: {message}");
                return;
            }

            var classname = parts[0];
            var callingMethodName = parts[1];
            var loadingProgramPath = parts[2];
            var interpretationMessage = parts[3];

            LogException(classname, callingMethodName, loadingProgramPath, interpretationMessage, e);
        }

        private static bool IsKnownWinProgramError(Exception e, string callingMethodName)
        {
            if (e.TargetSite?.Name == "GetDescription" && callingMethodName == "LnkProgram")
                return true;

            if (e is SecurityException || e is UnauthorizedAccessException || e is DirectoryNotFoundException)
                return true;

            return false;
        }

        private static bool IsKnownUWPProgramError(Exception e, string callingMethodName)
        {
            if (((e.HResult == -2147024774 || e.HResult == -2147009769) && callingMethodName == "ResourceFromPri")
                || (e.HResult == -2147024894 && (callingMethodName == "LogoPathFromUri" || callingMethodName == "ImageFromPath"))
                || (e.HResult == -2147024864 && callingMethodName == "InitializeAppInfo"))
                return true;

            if (callingMethodName == "XmlNamespaces")
                return true;

            return false;
        }
    }
}