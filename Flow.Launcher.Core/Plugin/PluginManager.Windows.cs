using Flow.Launcher.Infrastructure.DialogJump;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Plugin
{
    public static partial class PluginManager
    {
        static partial void InitializeDialogJumpPlugin(PluginPair pair) => DialogJump.InitializeDialogJumpPlugin(pair);
    }
}
