using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Flow.Launcher.Avalonia.Views.Controls
{
    public partial class CardGroup : ItemsControl
    {
        public CardGroup()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
