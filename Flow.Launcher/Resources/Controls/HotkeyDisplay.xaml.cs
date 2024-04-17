using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
using Windows.Media.Capture.Frames;
using static System.Windows.Forms.LinkLabel;
using static ABI.System.Collections.Generic.IReadOnlyDictionary_Delegates;

namespace Flow.Launcher.Resources.Controls
{
    public partial class HotkeyDisplay : UserControl
    {
        public HotkeyDisplay()
        {
            InitializeComponent();
            //List<string> stringList =e.NewValue.Split('+').ToList();
            Values = new ObservableCollection<string>();
            KeysControl.ItemsSource = Values;
        }

        public string Keys
        {
            get { return (string)GetValue(KeysValueProperty); }
            set { SetValue(KeysValueProperty, value); }
        }

        public static readonly DependencyProperty KeysValueProperty =
            DependencyProperty.Register("Keys", typeof(string), typeof(HotkeyDisplay),
                new PropertyMetadata(string.Empty, keyChanged));

        
        private static void keyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as UserControl;
            if (null == control) return; // This should not be possible

            var newValue = e.NewValue as string;
            if (null == newValue) return;

            if (d is not HotkeyDisplay hotkeyDisplay)
                return;

            hotkeyDisplay.Values.Clear();
            foreach (var key in newValue.Split('+'))
            {
                hotkeyDisplay.Values.Add(key);
            }
        }

        public ObservableCollection<string> Values { get; set; }
    }
}
