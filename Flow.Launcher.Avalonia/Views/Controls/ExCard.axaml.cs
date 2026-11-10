using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Flow.Launcher.Avalonia.Views.Controls
{
    public partial class ExCard : ContentControl
    {
        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<ExCard, string>(nameof(Title), string.Empty);

        public static readonly StyledProperty<string> SubProperty =
            AvaloniaProperty.Register<ExCard, string>(nameof(Sub), string.Empty);

        public static readonly StyledProperty<string> IconProperty =
            AvaloniaProperty.Register<ExCard, string>(nameof(Icon), string.Empty);

        public static readonly StyledProperty<object?> SideContentProperty =
            AvaloniaProperty.Register<ExCard, object?>(nameof(SideContent));

        public static readonly StyledProperty<bool> IsExpandedProperty =
            AvaloniaProperty.Register<ExCard, bool>(nameof(IsExpanded), false);

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

        public object? SideContent
        {
            get => GetValue(SideContentProperty);
            set => SetValue(SideContentProperty, value);
        }

        public bool IsExpanded
        {
            get => GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        public ExCard()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
