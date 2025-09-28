using System.ComponentModel;

namespace Flow.Launcher.Core.Resource
{
    public class LocalizedDescriptionAttribute : DescriptionAttribute
    {
        private readonly string _resourceKey;

        public LocalizedDescriptionAttribute(string resourceKey)
        {
            _resourceKey = resourceKey;
        }

        public override string Description
        {
            get
            {
                string description = PublicApi.Instance.GetTranslation(_resourceKey);
                return string.IsNullOrWhiteSpace(description) ? 
                    string.Format("[[{0}]]", _resourceKey) : description;
            }
        }
    }
}
