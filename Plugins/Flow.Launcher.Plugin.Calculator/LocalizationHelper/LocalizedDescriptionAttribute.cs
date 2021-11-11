using System.ComponentModel;

namespace Flow.Launcher.Plugin.Caculator.LocalizationHelper
{
    public class LocalizedDescriptionAttribute : DescriptionAttribute
    {
        private readonly IPublicAPI publicAPI;
        private readonly string _resourceKey;

        public LocalizedDescriptionAttribute(IPublicAPI api, string resourceKey)
        {
            publicAPI = api;
            _resourceKey = resourceKey;
        }

        public override string Description
        {
            get
            {
                string description = publicAPI.GetTranslation(_resourceKey);
                return string.IsNullOrWhiteSpace(description) ?
                    string.Format("[[{0}]]", _resourceKey) : description;
            }
        }
    }
}
