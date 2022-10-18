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
        private Settings.ProgramSource _updating;
        private Settings _settings;
        private bool update;

        public AddProgramSource(PluginInitContext context, Settings settings)
        {
            InitializeComponent();
            _context = context;
            _settings = settings;
            tbDirectory.Focus();
            Chkbox.IsChecked = true;
            update = false;
        }

        public AddProgramSource(Settings.ProgramSource source, Settings settings)
        {
            _updating = source;
            _settings = settings;
            update = true;
            InitializeComponent();
            Chkbox.IsChecked = _updating.Enabled;
            tbDirectory.Text = _updating.Location;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                tbDirectory.Text = dialog.SelectedPath;
            }
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnAdd_OnClick(object sender, RoutedEventArgs e)
        {
            string path = tbDirectory.Text;
            if (!System.IO.Directory.Exists(path))
            {
                System.Windows.MessageBox.Show(_context.API.GetTranslation("flowlauncher_plugin_program_invalid_path"));
                return;
            }
            if (!update)
            {
                if (!ProgramSetting.ProgramSettingDisplayList.Any(x => x.UniqueIdentifier == path))
                {
                    var source = new ProgramSource
                    {
                        Location = path,
                        UniqueIdentifier = path,
                        Enabled = Chkbox.IsChecked ?? true
                    };

                    _settings.ProgramSources.Insert(0, source);
                    ProgramSetting.ProgramSettingDisplayList.Add(source);
                }
            }
            else
            {
                _updating.Location = path;
                _updating.Enabled = Chkbox.IsChecked ?? true;
            }

            DialogResult = true;
            Close();
        }
    }
}
