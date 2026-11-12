using System.Diagnostics;
using Meziantou.Framework.Win32;

#nullable enable

namespace Flow.Launcher.Core.Plugin
{
    internal abstract partial class ProcessStreamPluginV2
    {
        private static readonly JobObject _jobObject = new();

        static ProcessStreamPluginV2()
        {
            _jobObject.SetLimits(new JobObjectLimits()
            {
                Flags = JobObjectLimitFlags.KillOnJobClose | JobObjectLimitFlags.DieOnUnhandledException |
                        JobObjectLimitFlags.SilentBreakawayOk
            });

            _jobObject.AssignProcess(Process.GetCurrentProcess());
        }

        partial void AssignToJobObject(Process process)
        {
            _jobObject.AssignProcess(process);
        }
    }
}
