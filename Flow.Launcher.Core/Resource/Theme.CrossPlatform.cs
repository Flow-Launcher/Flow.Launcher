using System;

namespace Flow.Launcher.Core.Resource
{
    public partial class Theme
    {
        // Theme XAML is never parsed into WPF resources on this platform, so XamlParseException cannot occur.
        private static partial bool IsXamlParseException(Exception e) => false;
    }
}
