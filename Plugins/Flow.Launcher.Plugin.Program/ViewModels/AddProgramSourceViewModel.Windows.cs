using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Flow.Launcher.Plugin.Program.Views;
using Flow.Launcher.Plugin.Program.Views.Models;

namespace Flow.Launcher.Plugin.Program.ViewModels
{
    public class AddProgramSourceViewModel : BaseModel
    {
        private readonly Settings Settings;

        private bool enabled = true;
        public bool Enabled
        {
            get => enabled;
            set
            {
                enabled = value;
                StatusModified = true;
            }
        }

        private string location = string.Empty;
        public string Location
        {
            get => location;
            set
            {
                location = value;
                LocationModified = true;
                OnPropertyChanged();
            }
        }

        public ProgramSource Source { get; init; }
        public IPublicAPI API { get; init; }
        public string AddBtnText { get; init; }
        private bool LocationModified = false;
        private bool StatusModified = false;
        public bool IsCustomSource { get; init; } = true;
        public bool IsNotCustomSource => !IsCustomSource;

        public AddProgramSourceViewModel(PluginInitContext context, Settings settings)
        {
            API = context.API;
            Settings = settings;
            AddBtnText = API.GetTranslation("flowlauncher_plugin_program_add");
        }

        public AddProgramSourceViewModel(PluginInitContext context, Settings settings, ProgramSource programSource) : this(context, settings)
        {
            Source = programSource;
            enabled = Source.Enabled;
            location = Source.Location;
            AddBtnText = API.GetTranslation("flowlauncher_plugin_program_update");
            IsCustomSource = Settings.ProgramSources.Any(x => x.UniqueIdentifier == Source.UniqueIdentifier);
        }

        public void Browse()
        {
            var dialog = new FolderBrowserDialog();
            DialogResult result = dialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                Location = dialog.SelectedPath;
            }
        }

        public (bool modified, string message) AddProgramSource()
        {
            if (!Directory.Exists(Location))
            {
                return (false, API.GetTranslation("flowlauncher_plugin_program_invalid_path"));
            }
            else if (DuplicateSource(Location))
            {
                return (false, API.GetTranslation("flowlauncher_plugin_program_duplicate_program_source"));
            }
            else
            {
                var source = new ProgramSource(Location, Enabled);
                Settings.ProgramSources.Insert(0, source);
                ProgramSetting.ProgramSettingDisplayList.Add(source);
                return (true, null);
            }
        }

        public (bool modified, string message) UpdateProgramSource()
        {
            if (LocationModified)
            {
                if (!Directory.Exists(Location))
                {
                    return (false, API.GetTranslation("flowlauncher_plugin_program_invalid_path"));
                }
                else if (DuplicateSource(Location))
                {
                    return (false, API.GetTranslation("flowlauncher_plugin_program_duplicate_program_source"));
                }
                else
                {
                    Source.Location = Location;  // Changes UniqueIdentifier internally
                }
            }
            if (StatusModified)
            {
                Source.Enabled = Enabled;
            }
            return (StatusModified || LocationModified, null);
        }

        public (bool modified, string message) AddOrUpdate()
        {
            if (Source == null)
            {
                return AddProgramSource();
            }
            else
            {
                return UpdateProgramSource();
            }
        }

        public static bool DuplicateSource(string location)
        {
            return ProgramSetting.ProgramSettingDisplayList.Any(x => x.UniqueIdentifier.Equals(location, StringComparison.OrdinalIgnoreCase));
        }
    }
}
