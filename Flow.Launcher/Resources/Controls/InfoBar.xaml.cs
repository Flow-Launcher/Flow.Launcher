using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Flow.Launcher.Resources.Controls
{
    public partial class InfoBar : UserControl
    {
        public InfoBar()
        {
            InitializeComponent();
            Loaded += InfoBar_Loaded;
        }

        private void InfoBar_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateStyle();
            UpdateTitleVisibility();
            UpdateMessageVisibility();
            UpdateOrientation();
            UpdateIconAlignmentAndMargin();
            UpdateIconVisibility();
            UpdateCloseButtonVisibility();
        }

        public static readonly DependencyProperty TypeProperty =
            DependencyProperty.Register(nameof(Type), typeof(InfoBarType), typeof(InfoBar), new PropertyMetadata(InfoBarType.Info, OnTypeChanged));

        public InfoBarType Type
        {
            get => (InfoBarType)GetValue(TypeProperty);
            set => SetValue(TypeProperty, value);
        }

        private static void OnTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is InfoBar infoBar)
            {
                infoBar.UpdateStyle();
            }
        }

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(nameof(Message), typeof(string), typeof(InfoBar), new PropertyMetadata(string.Empty, OnMessageChanged));

        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set
            {
                SetValue(MessageProperty, value);
            }
        }

        private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is InfoBar infoBar)
            {
                infoBar.UpdateMessageVisibility();
            }
        }

        private void UpdateMessageVisibility()
        {
            PART_Message.Visibility = string.IsNullOrEmpty(Message) ? Visibility.Collapsed : Visibility.Visible;
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(InfoBar), new PropertyMetadata(string.Empty, OnTitleChanged));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set
            {
                SetValue(TitleProperty, value);
                UpdateTitleVisibility(); // Visibility update when change Title
            }
        }

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is InfoBar infoBar)
            {
                infoBar.UpdateTitleVisibility();
            }
        }

        private void UpdateTitleVisibility()
        {
            PART_Title.Visibility = string.IsNullOrEmpty(Title) ? Visibility.Collapsed : Visibility.Visible;
        }

        public static readonly DependencyProperty IsIconVisibleProperty =
            DependencyProperty.Register(nameof(IsIconVisible), typeof(bool), typeof(InfoBar), new PropertyMetadata(true, OnIsIconVisibleChanged));

        public bool IsIconVisible
        {
            get => (bool)GetValue(IsIconVisibleProperty);
            set => SetValue(IsIconVisibleProperty, value);
        }

        public static readonly DependencyProperty LengthProperty =
            DependencyProperty.Register(nameof(Length), typeof(InfoBarLength), typeof(InfoBar), new PropertyMetadata(InfoBarLength.Short, OnLengthChanged));

        public InfoBarLength Length
        {
            get { return (InfoBarLength)GetValue(LengthProperty); }
            set { SetValue(LengthProperty, value); }
        }

        private static void OnLengthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is InfoBar infoBar)
            {
                infoBar.UpdateOrientation();
                infoBar.UpdateIconAlignmentAndMargin();
            }
        }

        private void UpdateOrientation()
        {
            PART_StackPanel.Orientation = Length == InfoBarLength.Long ? Orientation.Vertical : Orientation.Horizontal;
        }

        private void UpdateIconAlignmentAndMargin()
        {
            if (Length == InfoBarLength.Short)
            {
                PART_IconBorder.VerticalAlignment = VerticalAlignment.Center;
                PART_IconBorder.Margin = new Thickness(0, 0, 12, 0);
            }
            else
            {
                PART_IconBorder.VerticalAlignment = VerticalAlignment.Top;
                PART_IconBorder.Margin = new Thickness(0, 2, 12, 0);
            }
        }

        public static readonly DependencyProperty ClosableProperty =
            DependencyProperty.Register(nameof(Closable), typeof(bool), typeof(InfoBar), new PropertyMetadata(true, OnClosableChanged));

        public bool Closable
        {
            get => (bool)GetValue(ClosableProperty);
            set => SetValue(ClosableProperty, value);
        }

        private void PART_CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Visibility = Visibility.Collapsed;
        }

        private void UpdateStyle()
        {
            switch (Type)
            {
                case InfoBarType.Info:
                    PART_Border.Background = (Brush)FindResource("InfoBarInfoBG");
                    PART_IconBorder.Background = (Brush)FindResource("InfoBarInfoIcon");
                    PART_Icon.Glyph = "\xF13F";
                    break;
                case InfoBarType.Success:
                    PART_Border.Background = (Brush)FindResource("InfoBarSuccessBG");
                    PART_IconBorder.Background = (Brush)FindResource("InfoBarSuccessIcon");
                    PART_Icon.Glyph = "\xF13E";
                    break;
                case InfoBarType.Warning:
                    PART_Border.Background = (Brush)FindResource("InfoBarWarningBG");
                    PART_IconBorder.Background = (Brush)FindResource("InfoBarWarningIcon");
                    PART_Icon.Glyph = "\xF13C";
                    break;
                case InfoBarType.Error:
                    PART_Border.Background = (Brush)FindResource("InfoBarErrorBG");
                    PART_IconBorder.Background = (Brush)FindResource("InfoBarErrorIcon");
                    PART_Icon.Glyph = "\xF13D";
                    break;
                default:
                    PART_Border.Background = (Brush)FindResource("InfoBarInfoBG");
                    PART_IconBorder.Background = (Brush)FindResource("InfoBarInfoIcon");
                    PART_Icon.Glyph = "\xF13F";
                    break;
            }
        }

        private static void OnIsIconVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var infoBar = (InfoBar)d;
            infoBar.UpdateIconVisibility();
        }

        private static void OnClosableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var infoBar = (InfoBar)d;
            infoBar.UpdateCloseButtonVisibility();
        }

        private void UpdateIconVisibility()
        {
            PART_IconBorder.Visibility = IsIconVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateCloseButtonVisibility()
        {
            PART_CloseButton.Visibility = Closable ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public enum InfoBarType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public enum InfoBarLength
    {
        Short,
        Long
    }
}
