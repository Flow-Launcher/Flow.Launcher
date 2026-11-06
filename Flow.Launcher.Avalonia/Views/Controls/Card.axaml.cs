using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Flow.Launcher.Avalonia.Views.Controls
{
    public partial class Card : ContentControl
    {
        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<Card, string>(nameof(Title), string.Empty);

        public static readonly StyledProperty<string> SubProperty =
            AvaloniaProperty.Register<Card, string>(nameof(Sub), string.Empty);

        public static readonly StyledProperty<string> IconProperty =
            AvaloniaProperty.Register<Card, string>(nameof(Icon), string.Empty);

        public string Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Sub
        {
            get => GetValue(SubProperty);
            set => SetValue(SubProperty, value);
        }

        public string Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public Card()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
