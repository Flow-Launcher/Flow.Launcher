using System;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Flow.Launcher.Resources.Controls;
using NUnit.Framework;

namespace Flow.Launcher.Test;

[TestFixture]
public class MouseWheelTest
{
    [Test]
    [RequiresThread(System.Threading.ApartmentState.STA)]
    public void Test_Scroll_MouseWheel()
    {
        var scrollView = new CustomScrollViewerEx();

        var mouseDevice = Mouse.PrimaryDevice;
        var e = new MouseWheelEventArgs(mouseDevice, Environment.TickCount, 120)
        {
            RoutedEvent = UIElement.MouseWheelEvent
        };

        var onMouseWheelMethod = typeof(CustomScrollViewerEx).GetMethod(
            "OnMouseWheel",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        Assert.DoesNotThrow(() =>
        {
            onMouseWheelMethod.Invoke(scrollView, new object[] { e });
        });
    }
}
