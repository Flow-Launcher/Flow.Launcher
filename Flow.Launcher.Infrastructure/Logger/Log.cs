using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using NLog;
using NLog.Config;
using NLog.Targets;
using Flow.Launcher.Infrastructure.UserSettings;
using JetBrains.Annotations;
using NLog.Targets.Wrappers;
using System.Runtime.ExceptionServices;

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
            var fileTarget = new FileTarget
            {
                FileName = CurrentLogDirectory.Replace(@"\", "/") + "/${shortdate}.txt"
            };

            var fileTargetASyncWrapper = new AsyncTargetWrapper(fileTarget);

            var debugTarget = new DebuggerTarget
            {
                Layout = "${level:uppercase=true}|${message}"
            };
            
            configuration.AddTarget("file", fileTargetASyncWrapper);
            configuration.AddTarget("console", debugTarget);
#if DEBUG
            var fileRule = new LoggingRule("*", LogLevel.Debug, fileTargetASyncWrapper);
            var debugRule = new LoggingRule("*", LogLevel.Debug, debugTarget);
#else
            var rule = new LoggingRule("*", LogLevel.Info, fileTargetASyncWrapper);
            var debugRule = new LoggingRule("*", LogLevel.Info, consoleTarget);
#endif
            configuration.LoggingRules.Add(fileRule);
            configuration.LoggingRules.Add(debugRule);
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
            var valid = parts.Length == 3 && !string.IsNullOrWhiteSpace(parts[1]) && !string.IsNullOrWhiteSpace(parts[2]);
            return valid;
        }


        public static void Exception(string className, string message, System.Exception exception, [CallerMemberName] string methodName = "")
        {
            exception = exception.Demystify();
#if DEBUG
            ExceptionDispatchInfo.Capture(exception).Throw();
#else
            var classNameWithMethod = CheckClassAndMessageAndReturnFullClassWithMethod(className, message, methodName);

            ExceptionInternal(classNameWithMethod, message, exception);
#endif
        }

        private static string CheckClassAndMessageAndReturnFullClassWithMethod(string className, string message,
            string methodName)
        {
            if (string.IsNullOrWhiteSpace(className))
            {
                LogFaultyFormat($"Fail to specify a class name during logging of message: {message ?? "no message entered"}");
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                // todo: not sure we really need that
                LogFaultyFormat($"Fail to specify a message during logging");
            }

            if (!string.IsNullOrWhiteSpace(methodName))
            {
                return className + "." + methodName;
            }

            return className;
        }

        private static void ExceptionInternal(string classAndMethod, string message, System.Exception e)
        {
            var logger = LogManager.GetLogger(classAndMethod);


            logger.Error("-------------------------- Begin exception --------------------------");
            logger.Error(message);

            do
            {
                logger.Error($"Exception full name:\n <{e.GetType().FullName}>");
                logger.Error($"Exception message:\n <{e.Message}>");
                logger.Error($"Exception stack trace:\n <{e.StackTrace}>");
                logger.Error($"Exception source:\n <{e.Source}>");
                logger.Error($"Exception target site:\n <{e.TargetSite}>");
                logger.Error($"Exception HResult:\n <{e.HResult}>");
                e = e.InnerException;
            } while (e != null);

            logger.Error("-------------------------- End exception --------------------------");
        }

        private static void LogInternal(string message, LogLevel level)
        {
            if (FormatValid(message))
            {
                var parts = message.Split('|');
                var prefix = parts[1];
                var unprefixed = parts[2];
                var logger = LogManager.GetLogger(prefix);
                logger.Log(level, unprefixed);
            }
            else
            {
                LogFaultyFormat(message);
            }
        }

        /// <param name="message">example: "|prefix|unprefixed" </param>
        /// <param name="e">Exception</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void Exception(string message, System.Exception e)
        {
            e = e.Demystify();
#if DEBUG
            ExceptionDispatchInfo.Capture(e).Throw();
#else
            if (FormatValid(message))
            {
                var parts = message.Split('|');
                var prefix = parts[1];
                var unprefixed = parts[2];
                ExceptionInternal(prefix, unprefixed, e);
            }
            else
            {
                LogFaultyFormat(message);
            }
#endif
        }

        /// <param name="message">example: "|prefix|unprefixed" </param>
        public static void Error(string message)
        {
            LogInternal(message, LogLevel.Error);
        }

        public static void Error(string className, string message, [CallerMemberName] string methodName = "")
        {
            LogInternal(LogLevel.Error, className, message, methodName);
        }

        private static void LogInternal(LogLevel level, string className, string message, [CallerMemberName] string methodName = "")
        {
            var classNameWithMethod = CheckClassAndMessageAndReturnFullClassWithMethod(className, message, methodName);

            var logger = LogManager.GetLogger(classNameWithMethod);

            logger.Log(level, message);
        }

        public static void Debug(string className, string message, [CallerMemberName] string methodName = "")
        {
            LogInternal(LogLevel.Debug, className, message, methodName);
        }

        /// <param name="message">example: "|prefix|unprefixed" </param>
        public static void Debug(string message)
        {
            LogInternal(message, LogLevel.Debug);
        }

        public static void Info(string className, string message, [CallerMemberName] string methodName = "")
        {
            LogInternal(LogLevel.Info, className, message, methodName);
        }

        /// <param name="message">example: "|prefix|unprefixed" </param>
        public static void Info(string message)
        {
            LogInternal(message, LogLevel.Info);
        }

        public static void Warn(string className, string message, [CallerMemberName] string methodName = "")
        {
            LogInternal(LogLevel.Warn, className, message, methodName);
        }

        /// <param name="message">example: "|prefix|unprefixed" </param>
        public static void Warn(string message)
        {
            LogInternal(message, LogLevel.Warn);
        }
    }
}