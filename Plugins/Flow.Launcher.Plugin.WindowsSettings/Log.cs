using System;
using System.Runtime.CompilerServices;

namespace Flow.Launcher.Plugin.WindowsSettings
{
    public static class Log
    {
        private static IPublicAPI? _api;

        public static void Init(IPublicAPI api)
        {
            _api = api;
        }
        public static void Exception(string message, Exception exception, Type type, [CallerMemberName] string methodName = "")
        {
            _api?.LogException(type.FullName, message, exception, methodName);
        }
        public static void Warn(string message, Type type, [CallerMemberName] string methodName = "")
        {
            _api?.LogWarn(type.FullName, message, methodName);
        }
    }
}
