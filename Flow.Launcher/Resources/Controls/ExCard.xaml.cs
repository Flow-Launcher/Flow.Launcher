using System.Windows;
using System.Windows.Controls;

namespace Flow.Launcher.Resources.Controls
{
    public partial class ExCard : UserControl
    {
        public ExCard()
        {
            InitializeComponent();
        }
        public string Title
        {
            get { return (string)GetValue(TitleValueProperty); }
            set { SetValue(TitleValueProperty, value); }
        }
        public static readonly DependencyProperty TitleValueProperty =
          DependencyProperty.Register(nameof(Title), typeof(string), typeof(ExCard), new PropertyMetadata(string.Empty));

        public string Sub
        {
            get { return (string)GetValue(SubValueProperty); }
            set { SetValue(SubValueProperty, value); }
        }
        public static readonly DependencyProperty SubValueProperty =
          DependencyProperty.Register(nameof(Sub), typeof(string), typeof(ExCard), new PropertyMetadata(string.Empty));

        public string Icon
        {
            get { return (string)GetValue(IconValueProperty); }
            set { SetValue(IconValueProperty, value); }
        }
        public static readonly DependencyProperty IconValueProperty =
          DependencyProperty.Register(nameof(Icon), typeof(string), typeof(ExCard), new PropertyMetadata(string.Empty));

        /// <summary>
        /// Gets or sets additional content for the UserControl
        /// </summary>
        public object AdditionalContent
        {
            get { return (object)GetValue(AdditionalContentProperty); }
            set { SetValue(AdditionalContentProperty, value); }
        }
        public static readonly DependencyProperty AdditionalContentProperty =
            DependencyProperty.Register(nameof(AdditionalContent), typeof(object), typeof(ExCard),
              new PropertyMetadata(null));

        public object SideContent
        {
            get { return (object)GetValue(SideContentProperty); }
            set { SetValue(SideContentProperty, value); }
        }
        public static readonly DependencyProperty SideContentProperty =
            DependencyProperty.Register(nameof(SideContent), typeof(object), typeof(ExCard),
              new PropertyMetadata(null));
    }
}
