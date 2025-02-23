using CommunityToolkit.Mvvm.DependencyInjection;

namespace Flow.Launcher.Core.Resource
{
    public static class InternationalizationManager
    {
        public static Internationalization Instance
            => Ioc.Default.GetRequiredService<Internationalization>();
    }
}
