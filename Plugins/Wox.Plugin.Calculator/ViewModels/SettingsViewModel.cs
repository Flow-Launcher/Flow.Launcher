using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Plugin.Caculator.ViewModels
{
    public class SettingsViewModel : BaseModel, ISavable
    {
        private readonly PluginJsonStorage<Settings> _storage;

        public SettingsViewModel()
        {
            _storage = new PluginJsonStorage<Settings>();
            Settings = _storage.Load();
        }

        public Settings Settings { get; set; }

        public IEnumerable<int> MaxDecimalPlacesRange => Enumerable.Range(1, 20);

        public void Save()
        {
            _storage.Save();
        }
    }
}
