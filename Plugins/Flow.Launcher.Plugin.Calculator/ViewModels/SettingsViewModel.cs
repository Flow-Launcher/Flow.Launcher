using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.Caculator.ViewModels
{
    public class SettingsViewModel : BaseModel
    {
        public SettingsViewModel(Settings settings)
        {
            Settings = settings;
        }

        public Settings Settings { get; init; }

        public IEnumerable<int> MaxDecimalPlacesRange => Enumerable.Range(1, 20);
    }
}
