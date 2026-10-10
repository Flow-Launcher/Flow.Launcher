using System.Windows.Input;
using Flow.Launcher.ViewModel;
using NUnit.Framework;

namespace Flow.Launcher.Test;

[TestFixture]
public class GridKeyRoutingTest
{
    [TestCase(Key.Down, false, GridKeyAction.EnterGrid)]
    [TestCase(Key.Down, true, GridKeyAction.NextRow)]
    [TestCase(Key.Up, false, GridKeyAction.Consume)]
    [TestCase(Key.Up, true, GridKeyAction.PrevRow)]
    [TestCase(Key.Right, false, GridKeyAction.NotHandled)]
    [TestCase(Key.Right, true, GridKeyAction.NextCell)]
    [TestCase(Key.Left, false, GridKeyAction.NotHandled)]
    [TestCase(Key.Left, true, GridKeyAction.PrevCell)]
    [TestCase(Key.Tab, false, GridKeyAction.EnterGrid)]
    [TestCase(Key.Tab, true, GridKeyAction.LeaveGrid)]
    [TestCase(Key.Escape, false, GridKeyAction.NotHandled)]
    [TestCase(Key.Escape, true, GridKeyAction.LeaveGrid)]
    [TestCase(Key.Enter, true, GridKeyAction.NotHandled)]
    [TestCase(Key.A, true, GridKeyAction.NotHandled)]
    public void UnmodifiedKeys_RouteByControlOwner(Key key, bool gridHasControl, GridKeyAction expected)
    {
        Assert.That(GridKeyRouting.Route(key, ModifierKeys.None, gridHasControl), Is.EqualTo(expected));
    }

    [TestCase(false, GridKeyAction.Consume)]
    [TestCase(true, GridKeyAction.LeaveGrid)]
    public void ShiftTab_AlwaysReturnsControlToSearchBox(bool gridHasControl, GridKeyAction expected)
    {
        Assert.That(GridKeyRouting.Route(Key.Tab, ModifierKeys.Shift, gridHasControl), Is.EqualTo(expected));
    }

    [TestCase(Key.Tab, false)]
    [TestCase(Key.Tab, true)]
    [TestCase(Key.Down, true)]
    [TestCase(Key.Up, true)]
    [TestCase(Key.Left, true)]
    [TestCase(Key.Right, true)]
    [TestCase(Key.Escape, true)]
    public void ControlModifiedKeys_AreLeftToExistingBindings(Key key, bool gridHasControl)
    {
        Assert.That(GridKeyRouting.Route(key, ModifierKeys.Control, gridHasControl), Is.EqualTo(GridKeyAction.NotHandled));
    }

    [Test]
    public void ShiftArrow_IsLeftToTextSelection()
    {
        Assert.That(GridKeyRouting.Route(Key.Right, ModifierKeys.Shift, false), Is.EqualTo(GridKeyAction.NotHandled));
    }
}
