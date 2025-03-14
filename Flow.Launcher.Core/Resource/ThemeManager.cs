using System;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Flow.Launcher.Core.Resource
{
    [Obsolete("ThemeManager.Instance is obsolete. Use Ioc.Default.GetRequiredService<Theme>() instead.")]
    public class ThemeManager
    {
        public static Theme Instance
            => Ioc.Default.GetRequiredService<Theme>();
    }
}
