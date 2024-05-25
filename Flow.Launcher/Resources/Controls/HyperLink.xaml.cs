using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace Flow.Launcher.Resources.Controls;

public partial class HyperLink : UserControl
{
    public static readonly DependencyProperty UriProperty = DependencyProperty.Register(
        nameof(Uri), typeof(string), typeof(HyperLink), new PropertyMetadata(default(string))
    );

    public string Uri
    {
        get => (string)GetValue(UriProperty);
        set => SetValue(UriProperty, value);
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(HyperLink), new PropertyMetadata(default(string))
    );

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public HyperLink()
    {
        InitializeComponent();
    }

    private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        App.API.OpenUrl(e.Uri);
        e.Handled = true;
    }
}
