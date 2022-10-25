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

        public AddProgramSource(ProgramSource edit, Settings settings)
        {
            InitializeComponent();
            _updating = source;
            _settings = settings;
            update = true;
            Chkbox.IsChecked = _updating.Enabled;
            Directory.Text = _updating.Location;
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
            if (!System.IO.Directory.Exists(path))
            {
                System.Windows.MessageBox.Show(_context.API.GetTranslation("flowlauncher_plugin_program_invalid_path"));
                return;
            }
            if (!update)
            {
                if (!ProgramSetting.ProgramSettingDisplayList.Any(x => x.UniqueIdentifier.Equals(path, System.StringComparison.InvariantCultureIgnoreCase)))
                {
                    var source = new ProgramSource(path);

                    _settings.ProgramSources.Insert(0, source);
                    ProgramSetting.ProgramSettingDisplayList.Add(source);
                }
            }
            else
            {
                _updating.Location = path;
                _updating.Enabled = Chkbox.IsChecked ?? true;  // Fixme, need to add to disabled source if not custom source
            }

            DialogResult = true;
            Close();
        }
    }
}
