using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using NLog;
using NLog.Config;
using NLog.Targets;
using Flow.Launcher.Infrastructure.UserSettings;
using JetBrains.Annotations;
using NLog.Fluent;
using NLog.Targets.Wrappers;
using System.Runtime.ExceptionServices;
using System.Text;

namespace Flow.Launcher.Infrastructure.Logger
{
    public static class Log
    {
        public const string DirectoryName = "Logs";

        public static string CurrentLogDirectory { get; }

        static Log()
        {
            CurrentLogDirectory = Path.Combine(DataLocation.DataDirectory(), DirectoryName, Constant.Version);
            if (!Directory.Exists(CurrentLogDirectory))
            {
                Directory.CreateDirectory(CurrentLogDirectory);
            }

            var configuration = new LoggingConfiguration();

            const string layout =
                @"${logger}->${time}|${level}|${message}|${onexception:inner=${logger}->${newline}${date:format=HH\:mm\:ss}|${level}|${message}|${newline}${exception:format=toString}${newline}";

            var fileTarget = new FileTarget
            {
                FileName = CurrentLogDirectory.Replace(@"\", "/") + "/${shortdate}.txt",
                Layout = layout
            };

            var fileTargetASyncWrapper = new AsyncTargetWrapper(fileTarget);

            var debugTarget = new OutputDebugStringTarget
            {
                Layout = layout
            };

            configuration.AddTarget("file", fileTargetASyncWrapper);
            configuration.AddTarget("debug", debugTarget);

#if DEBUG
            var fileRule = new LoggingRule("*", LogLevel.Debug, fileTargetASyncWrapper);
            var debugRule = new LoggingRule("*", LogLevel.Debug, debugTarget);
            configuration.LoggingRules.Add(debugRule);
#else
            var fileRule = new LoggingRule("*", LogLevel.Info, fileTargetASyncWrapper);
#endif
            configuration.LoggingRules.Add(fileRule);
            LogManager.Configuration = configuration;
        }

        private static void LogFaultyFormat(string message)
        {
            var logger = LogManager.GetLogger("FaultyLogger");
            message = $"Wrong logger message format <{message}>";
            logger.Fatal(message);
        }

        private static bool FormatValid(string message)
        {
            var parts = message.Split('|');
            var valid = parts.Length == 3 && !string.IsNullOrWhiteSpace(parts[1]) &&
                        !string.IsNullOrWhiteSpace(parts[2]);
            return valid;
        }


        public static void Exception(string className, string message, System.Exception exception,
            [CallerMemberName] string methodName = "")
        {
            exception = exception.Demystify();
#if DEBUG
            ExceptionDispatchInfo.Capture(exception).Throw();
#else

            LogInternal(LogLevel.Error, className, message, methodName, exception);
#endif
        }


        public static void Error(string className, string message, [CallerMemberName] string methodName = "")
        {
            LogInternal(LogLevel.Error, className, message, methodName);
        }

        private static void LogInternal(LogLevel level, string className, string message, string methodName = "",
            System.Exception e = null)
        {
            var logger = LogManager.GetLogger(className);
           
            logger.Log(level, e, "{MethodName:l}|{Message}", methodName, message);
        }

        [Conditional("DEBUG")]
        public static void Debug(string className, string message, [CallerMemberName] string methodName = "")
        {
            LogInternal(LogLevel.Debug, className, message, methodName);
        }

        public static void Info(string className, string message, [CallerMemberName] string methodName = "")
        {
            LogInternal(LogLevel.Info, className, message, methodName);
        }


        public static void Warn(string className, string message, [CallerMemberName] string methodName = "")
        {
            LogInternal(LogLevel.Warn, className, message, methodName);
        }
    }
}