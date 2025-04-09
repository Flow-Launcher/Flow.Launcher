using System;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Infrastructure
{
    public static class Stopwatch
    {
        /// <summary>
        /// This stopwatch will appear only in Debug mode
        /// </summary>
        public static long Debug(string message, Action action)
        {
            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            action();
            stopWatch.Stop();
            var milliseconds = stopWatch.ElapsedMilliseconds;
            string info = $"{message} <{milliseconds}ms>";
            Log.Debug(info);
            return milliseconds;
        }
        
        /// <summary>
        /// This stopwatch will appear only in Debug mode
        /// </summary>
        public static async Task<long> DebugAsync(string message, Func<Task> action)
        {
            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            await action();
            stopWatch.Stop();
            var milliseconds = stopWatch.ElapsedMilliseconds;
            string info = $"{message} <{milliseconds}ms>";
            Log.Debug(info);
            return milliseconds;
        }
        
        public static long Normal(string message, Action action)
        {
            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            action();
            stopWatch.Stop();
            var milliseconds = stopWatch.ElapsedMilliseconds;
            string info = $"{message} <{milliseconds}ms>";
            Log.Info(info);
            return milliseconds;
        }
        
        public static async Task<long> NormalAsync(string message, Func<Task> action)
        {
            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            await action();
            stopWatch.Stop();
            var milliseconds = stopWatch.ElapsedMilliseconds;
            string info = $"{message} <{milliseconds}ms>";
            Log.Info(info);
            return milliseconds;
        }
    }
}
