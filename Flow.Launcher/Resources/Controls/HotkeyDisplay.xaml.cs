using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Flow.Launcher.Resources.Controls
{
    public partial class HotkeyDisplay : UserControl
    {
        public enum DisplayType
        {
            Default,
            Small
        }

        public HotkeyDisplay()
        {
            InitializeComponent();
            //List<string> stringList =e.NewValue.Split('+').ToList();
            Values = new ObservableCollection<string>();
            KeysControl.ItemsSource = Values;
        }

        public string Keys
        {
            get { return (string)GetValue(KeysProperty); }
            set { SetValue(KeysProperty, value); }
        }

        public static readonly DependencyProperty KeysProperty =
            DependencyProperty.Register(nameof(Keys), typeof(string), typeof(HotkeyDisplay),
                new PropertyMetadata(string.Empty, keyChanged));

        public DisplayType Type
        {
            get { return (DisplayType)GetValue(TypeProperty); }
            set { SetValue(TypeProperty, value); }
        }

        public static readonly DependencyProperty TypeProperty =
            DependencyProperty.Register(nameof(Type), typeof(DisplayType), typeof(HotkeyDisplay),
                new PropertyMetadata(DisplayType.Default));

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
