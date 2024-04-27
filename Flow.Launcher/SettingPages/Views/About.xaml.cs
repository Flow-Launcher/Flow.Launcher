using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows;
using System.Windows.Navigation;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Path = System.IO.Path;

namespace Flow.Launcher.SettingPages.Views
{

    public partial class About
    {
        public About()
        {
            InitializeComponent();
        }
        private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            //API.OpenUrl(e.Uri.AbsoluteUri);
            e.Handled = true;
        }
    }
}
