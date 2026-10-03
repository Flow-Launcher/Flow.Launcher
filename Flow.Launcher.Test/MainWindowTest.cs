using System.Reflection;
using NUnit.Framework;

namespace Flow.Launcher.Test;

public class MainWindowTest
{
    [Test]
    public void ShouldSuppressWindowAutomationMessage_ForWmGetObjectOnly()
    {
        var method = typeof(MainWindow).GetMethod("ShouldSuppressWindowAutomationMessage", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(method, Is.Not.Null);
        Assert.That((bool)method.Invoke(null, [0x003D]), Is.True);
        Assert.That((bool)method.Invoke(null, [0x0112]), Is.False);
    }
}
