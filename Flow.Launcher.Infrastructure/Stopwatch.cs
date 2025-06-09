using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Infrastructure
{
    public static class Stopwatch
    {
        /// <summary>
        /// This stopwatch will appear only in Debug mode
        /// </summary>
        public static long Debug(string className, string message, Action action, [CallerMemberName] string methodName = "")
        {
            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            action();
            stopWatch.Stop();
            var milliseconds = stopWatch.ElapsedMilliseconds;
            Log.Debug(className, $"{message} <{milliseconds}ms>", methodName);
            return milliseconds;
        }
        
        /// <summary>
        /// This stopwatch will appear only in Debug mode
        /// </summary>
        public static async Task<long> DebugAsync(string className, string message, Func<Task> action, [CallerMemberName] string methodName = "")
        {
            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            await action();
            stopWatch.Stop();
            var milliseconds = stopWatch.ElapsedMilliseconds;
            Log.Debug(className, $"{message} <{milliseconds}ms>", methodName);
            return milliseconds;
        }
        
        public static long Info(string className, string message, Action action, [CallerMemberName] string methodName = "")
        {
            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            action();
            stopWatch.Stop();
            var milliseconds = stopWatch.ElapsedMilliseconds;
            Log.Info(className, $"{message} <{milliseconds}ms>", methodName);
            return milliseconds;
        }
        
        public static async Task<long> InfoAsync(string className, string message, Func<Task> action, [CallerMemberName] string methodName = "")
        {
            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            await action();
            stopWatch.Stop();
            var milliseconds = stopWatch.ElapsedMilliseconds;
            Log.Info(className, $"{message} <{milliseconds}ms>", methodName);
            return milliseconds;
        }
    }
}
