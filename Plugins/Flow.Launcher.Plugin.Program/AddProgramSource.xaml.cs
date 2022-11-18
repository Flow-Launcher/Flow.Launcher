using System.Windows;
using System.Windows.Forms;
using Flow.Launcher.Plugin.Program.Views.Models;
using Flow.Launcher.Plugin.Program.Views;
using System.Linq;

namespace Flow.Launcher.Plugin.Program
{
    /// <summary>
    /// Interaction logic for AddProgramSource.xaml
    /// </summary>
    public partial class AddProgramSource : Window
    {
        private PluginInitContext _context;
        private ProgramSource _editing;
        private Settings _settings;
        private bool update;

        public AddProgramSource(PluginInitContext context, Settings settings)
        {
            InitializeComponent();
            _context = context;
            _settings = settings;
            Directory.Focus();
            Chkbox.IsChecked = true;
            update = false;
            btnAdd.Content = _context.API.GetTranslation("flowlauncher_plugin_program_add");
        }

        public AddProgramSource(PluginInitContext context, Settings settings, ProgramSource source)
        {
            InitializeComponent();
            _context = context;
            _editing = source;
            _settings = settings;
            update = true;
            Chkbox.IsChecked = _editing.Enabled;
            Directory.Text = _editing.Location;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                Directory.Text = dialog.SelectedPath;
            }
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnAdd_OnClick(object sender, RoutedEventArgs e)
        {
            string path = Directory.Text;
            bool modified = false;
            if (!System.IO.Directory.Exists(path))
            {
                System.Windows.MessageBox.Show(_context.API.GetTranslation("flowlauncher_plugin_program_invalid_path"));
                return;
            }
            if (!update)
            {
                if (!ProgramSetting.ProgramSettingDisplayList.Any(x => x.UniqueIdentifier.Equals(path, System.StringComparison.OrdinalIgnoreCase)))
                {
                    var source = new ProgramSource(path);
                    modified = true;
                    _settings.ProgramSources.Insert(0, source);
                    ProgramSetting.ProgramSettingDisplayList.Add(source);
                }
                else
                {
                    System.Windows.MessageBox.Show(_context.API.GetTranslation("flowlauncher_plugin_program_duplicate_program_source"));
                    return;
                }
            }
            else
            {
                // Separate checks to avoid changing UniqueIdentifier of UWP
                if (!_editing.Location.Equals(path, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (ProgramSetting.ProgramSettingDisplayList
                            .Any(x => x.UniqueIdentifier.Equals(path, System.StringComparison.OrdinalIgnoreCase)))
                    {
                        // Check if the new location is used
                        // No need to check win32 or uwp, just override them
                        System.Windows.MessageBox.Show(_context.API.GetTranslation("flowlauncher_plugin_program_duplicate_program_source"));
                        return;
                    }
                    modified = true;
                    _editing.Location = path;  // Changes UniqueIdentifier internally
                }
                if (_editing.Enabled != Chkbox.IsChecked)
                {
                    modified = true;
                    _editing.Enabled = Chkbox.IsChecked ?? true;
                }
            }

            DialogResult = modified;
            Close();
        }
    }
}
