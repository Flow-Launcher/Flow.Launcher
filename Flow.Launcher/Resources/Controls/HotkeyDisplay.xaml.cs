using System;
using System.Collections.Generic;
using System.Globalization;
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

namespace Flow.Launcher.Resources.Controls
{
    public partial class HotkeyDisplay : UserControl
    {
        public HotkeyDisplay()
        {
            InitializeComponent();
        }

        public string Keys
        {
            get { return (string)GetValue(KeysValueProperty); }
            set { SetValue(KeysValueProperty, value); }
        }
        public static readonly DependencyProperty KeysValueProperty =
          DependencyProperty.Register("Keys", typeof(string), typeof(HotkeyDisplay), new PropertyMetadata(string.Empty));
    }
}
