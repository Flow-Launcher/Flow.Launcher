using System.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Resource
{
    public class LocalizedDescriptionAttribute : DescriptionAttribute
    {
        // We should not initialize API in static constructor because it will create another API instance
        private static IPublicAPI api = null;
        private static IPublicAPI API => api ??= Ioc.Default.GetRequiredService<IPublicAPI>();

        private readonly string _resourceKey;

        public LocalizedDescriptionAttribute(string resourceKey)
        {
            _resourceKey = resourceKey;
        }

        public override string Description
        {
            get
            {
                string description = API.GetTranslation(_resourceKey);
                return string.IsNullOrWhiteSpace(description) ? 
                    string.Format("[[{0}]]", _resourceKey) : description;
            }
        }
    }
}
