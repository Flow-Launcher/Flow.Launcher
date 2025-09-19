using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Flow.Launcher.Infrastructure.UserSettings;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace Flow.Launcher.Infrastructure.Logger
{
    public static class Log
    {
        public const string DirectoryName = Constant.Logs;

        public static string CurrentLogDirectory { get; }

        static Log()
        {
            CurrentLogDirectory = DataLocation.VersionLogDirectory;
            if (!Directory.Exists(CurrentLogDirectory))
            {
                Directory.CreateDirectory(CurrentLogDirectory);
            }

            var configuration = new LoggingConfiguration();

            const string layout = 
                @"${date:format=HH\:mm\:ss.ffffK} - " +
                @"${level:uppercase=true:padding=-5} - ${logger} - ${message:l}" +
                @"${onexception:${newline}" +
                    @"EXCEPTION OCCURS\: ${exception:format=tostring}${newline}}";
            
            var fileTarget = new FileTarget
            {
                FileName = CurrentLogDirectory.Replace(@"\", "/") + "/Flow.Launcher.${date:format=yyyy-MM-dd}.log",
                Layout = layout
            };

            var fileTargetASyncWrapper = new AsyncTargetWrapper(fileTarget);

            var debugTarget = new OutputDebugStringTarget
            {
                Layout = layout
            };

            configuration.AddTarget("file", fileTargetASyncWrapper);
            configuration.AddTarget("debug", debugTarget);

            var fileRule = new LoggingRule("*", LogLevel.Debug, fileTargetASyncWrapper)
            {
                RuleName = "file"
            };
#if DEBUG
            var debugRule = new LoggingRule("*", LogLevel.Debug, debugTarget)
            {
                RuleName = "debug"
            };
            configuration.LoggingRules.Add(debugRule);
#endif
            configuration.LoggingRules.Add(fileRule);
            LogManager.Configuration = configuration;
        }

        public static void SetLogLevel(LOGLEVEL level)
        {
            var rule = LogManager.Configuration.FindRuleByName("file");

            var nlogLevel = level switch
            {
                LOGLEVEL.NONE => LogLevel.Off,
                LOGLEVEL.ERROR => LogLevel.Error,
                LOGLEVEL.DEBUG => LogLevel.Debug,
                _ => LogLevel.Info
            };

            rule.SetLoggingLevels(nlogLevel, LogLevel.Fatal);

            LogManager.ReconfigExistingLoggers();

            // We can't log Info when level is set to Error or None, so we use Debug
            Debug(nameof(Logger), $"Using log level: {level}.");
        }

        private static void LogFaultyFormat(string message)
        {
            var logger = LogManager.GetLogger("FaultyLogger");
            message = $"Wrong logger message format <{message}>";
            logger.Fatal(message);
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

#if !DEBUG
        private static void ExceptionInternal(string classAndMethod, string message, System.Exception e)
        {
            var logger = LogManager.GetLogger(classAndMethod);

            logger.Error(e, message);
        }
#endif

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

        public static void Info(string className, string message, [CallerMemberName] string methodName = "")
        {
            LogInternal(LogLevel.Info, className, message, methodName);
        }

        public static void Warn(string className, string message, [CallerMemberName] string methodName = "")
        {
            LogInternal(LogLevel.Warn, className, message, methodName);
        }
    }

    public enum LOGLEVEL
    {
        NONE,
        ERROR,
        INFO,
        DEBUG
    }
}
