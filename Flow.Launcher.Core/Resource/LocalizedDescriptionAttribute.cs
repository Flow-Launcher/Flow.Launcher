using System.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Resource
{
    public class LocalizedDescriptionAttribute : DescriptionAttribute
    {
        private static readonly IPublicAPI _api = Ioc.Default.GetRequiredService<IPublicAPI>();
        private readonly string _resourceKey;

        public LocalizedDescriptionAttribute(string resourceKey)
        {
            _resourceKey = resourceKey;
        }

        public override string Description
        {
            get
            {
                string description = _api.GetTranslation(_resourceKey);
                return string.IsNullOrWhiteSpace(description) ? 
                    string.Format("[[{0}]]", _resourceKey) : description;
            }
        }
    }
}
