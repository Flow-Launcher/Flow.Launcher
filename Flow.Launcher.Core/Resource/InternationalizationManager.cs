using System;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Flow.Launcher.Core.Resource
{
    [Obsolete("InternationalizationManager.Instance is obsolete. Use Ioc.Default.GetRequiredService<Internationalization>() instead.")]
    public static class InternationalizationManager
    {
        public static Internationalization Instance
            => Ioc.Default.GetRequiredService<Internationalization>();
    }
}
