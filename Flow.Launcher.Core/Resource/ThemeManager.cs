using CommunityToolkit.Mvvm.DependencyInjection;

namespace Flow.Launcher.Core.Resource
{
    public class ThemeManager
    {
        public static Theme Instance
            => Ioc.Default.GetRequiredService<Theme>();
    }
}
