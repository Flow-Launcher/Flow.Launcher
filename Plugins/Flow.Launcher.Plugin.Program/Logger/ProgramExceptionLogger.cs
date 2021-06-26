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
using System.Text;

namespace Flow.Launcher.Plugin.Program.Logger
{
    /// <summary>
    /// The Program plugin has seen many issues recorded in the Flow Launcher repo related to various loading of Windows programs.
    /// This is a dedicated logger for this Program plugin with the aim to output a more friendlier message and clearer
    /// log that will allow debugging to be quicker and easier.
    /// </summary>
    internal static class ProgramExceptionLogger
    {
        internal static IPublicAPI Api { private get; set; }

        /// <summary>
        /// Logs an exception
        /// </summary>
        internal static void LogException(string classname, string loadingProgramPath,
            string interpretationMessage, Exception e, [CallerMemberName] string callingMethodName = "")
        {
            var possibleResolution = "Possible Resolution: Not yet known";

            bool known = false;

            StringBuilder messageBuilder = new StringBuilder();

            messageBuilder.AppendLine(interpretationMessage);

            if (IsKnownWinProgramError(e, callingMethodName) || IsKnownUWPProgramError(e, callingMethodName))
            {
                possibleResolution = "Possible Resolution: Can be ignored and Flow Launcher should still continue, however the program may not be loaded";
                known = true;
            }

            messageBuilder.AppendLine(possibleResolution);
            messageBuilder.Append($"Program Path: {loadingProgramPath}");

            if (known)
                Api.LogWarn($"Flow.Plugin.Program.{classname}", messageBuilder.ToString(), e, callingMethodName);
            else
                Api.LogException($"Flow.Plugin.Program.{classname}", messageBuilder.ToString(), e, callingMethodName);

        }

        private static bool IsKnownWinProgramError(Exception e, string callingMethodName) => e switch
        {
            { TargetSite: { Name: "GetDescription" } } when callingMethodName is "LnkProgram" => true,
            SecurityException or UnauthorizedAccessException or DirectoryNotFoundException => true,
            _ => false,
        };

        private static bool IsKnownUWPProgramError(Exception e, string callingMethodName) => e.HResult switch
        {
            -2147024774 or -2147009769 => callingMethodName == "ResourceFromPri",
            -2147024894 => callingMethodName is "LogoPathFromUri" or "ImageFromPath",
            -2147024864 => callingMethodName == "InitializeAppInfo",
            _ => callingMethodName is "XmlNamespaces" or "InitPackageVersion"
        };
    }
}