using Flow.Launcher.Infrastructure.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Flow.Launcher.Plugin.Explorer.ViewModels
{
    public class SettingsViewModel
    {
        private readonly PluginJsonStorage<Settings> _storage;

        public Settings Settings { get; set; }

        public SettingsViewModel()
        {
            _storage = new PluginJsonStorage<Settings>();
            Settings = _storage.Load();
        }

        public void Save()
        {
            _storage.Save();
        }
    }
}
