using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Infrastructure
{
    public static class Stopwatch
    {
        private static readonly Dictionary<string, long> Count = new();
        private static readonly object Locker = new();

        /// <summary>
        /// This stopwatch will appear only in Debug mode
        /// </summary>
        public static long Debug(string className, string message, Action action, [CallerMemberName]string methodName = "")
        {
            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            action();
            stopWatch.Stop();
            var milliseconds = stopWatch.ElapsedMilliseconds;
            string info = $"{message} <{milliseconds}ms>";
            Log.Debug(className, info, methodName);
            return milliseconds;
        }

        /// <summary>
        /// This stopwatch will appear only in Debug mode
        /// </summary>
        public static async Task<long> DebugAsync(string className,string message, Func<Task> action,[CallerMemberName]string methodName = "")
        {
            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            await action();
            stopWatch.Stop();
            var milliseconds = stopWatch.ElapsedMilliseconds;
            string info = $"{message} <{milliseconds}ms>";
            Log.Debug(className, info, methodName);
            return milliseconds;
        }

        public static long Normal(string className, string message, Action action, [CallerMemberName]string methodName = "")
        {
            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            action();
            stopWatch.Stop();
            var milliseconds = stopWatch.ElapsedMilliseconds;
            string info = $"{message} <{milliseconds}ms>";
            Log.Info(className, info, methodName);
            return milliseconds;
        }

        public static async Task<long> NormalAsync(string className,string message, Func<Task> action,[CallerMemberName]string methodName = "")
        {
            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            await action();
            stopWatch.Stop();
            var milliseconds = stopWatch.ElapsedMilliseconds;
            string info = $"{message} <{milliseconds}ms>";
            Log.Info(className, info, methodName);
            return milliseconds;
        }


        public static void StartCount(string name, Action action)
        {
            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            action();
            stopWatch.Stop();
            var milliseconds = stopWatch.ElapsedMilliseconds;
            lock (Locker)
            {
                if (Count.ContainsKey(name))
                {
                    Count[name] += milliseconds;
                }
                else
                {
                    Count[name] = 0;
                }
            }
        }

        public static void EndCount()
        {
            foreach (var key in Count.Keys)
            {
                string info = $"{key} already cost {Count[key]}ms";
                Log.Debug("", info, "");
            }
        }
    }
}