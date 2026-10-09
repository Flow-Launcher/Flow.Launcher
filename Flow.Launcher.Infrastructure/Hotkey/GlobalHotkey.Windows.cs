namespace Flow.Launcher.Infrastructure.Hotkey
{
    public unsafe partial class GlobalHotkey
    {
        public static System.Windows.Input.Key GetKeyFromVk(int vkCode)
        {
            return System.Windows.Input.KeyInterop.KeyFromVirtualKey(vkCode);
        }
    }
}
