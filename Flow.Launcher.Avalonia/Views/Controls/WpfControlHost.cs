using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using System.Windows.Interop;
using System.Windows;

namespace Flow.Launcher.Avalonia.Views.Controls
{
    public class WpfControlHost : NativeControlHost
    {
        private HwndSource? _source;

        public static readonly StyledProperty<object?> ContentProperty =
            AvaloniaProperty.Register<WpfControlHost, object?>(nameof(Content));

        public object? Content
        {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
        {
            var parameters = new HwndSourceParameters
            {
                ParentWindow = parent.Handle,
                WindowStyle = 0x50000000, // WS_CHILD | WS_VISIBLE
                PositionX = 0,
                PositionY = 0,
            };

            _source = new HwndSource(parameters);
            
            if (Content != null)
            {
                _source.RootVisual = Content as System.Windows.Media.Visual;
            }

            return new PlatformHandle(_source.Handle, "HwndSource");
        }

        protected override void DestroyNativeControlCore(IPlatformHandle control)
        {
            _source?.Dispose();
            _source = null;
            base.DestroyNativeControlCore(control);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ContentProperty && _source != null)
            {
                _source.RootVisual = change.NewValue as System.Windows.Media.Visual;
            }
        }
    }
}
