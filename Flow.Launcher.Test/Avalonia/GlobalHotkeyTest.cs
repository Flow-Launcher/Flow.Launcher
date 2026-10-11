using Flow.Launcher.Avalonia.Helper;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test.Avalonia
{
    [TestFixture]
    public class GlobalHotkeyTest
    {
        [TestCase("Ctrl + F1", GlobalHotkey.Modifiers.Control, 0x70u)]
        [TestCase("Alt + D0", GlobalHotkey.Modifiers.Alt, 0x30u)]
        [TestCase("Shift + Up", GlobalHotkey.Modifiers.Shift, 0x26u)]
        public void ParseHotkeyString_WhenKeyUsesWpfKeyName_ReturnsExpectedVirtualKey(
            string hotkeyString,
            GlobalHotkey.Modifiers expectedModifiers,
            uint expectedKey)
        {
            var (modifiers, key) = GlobalHotkey.ParseHotkeyString(hotkeyString);

            ClassicAssert.AreEqual(expectedModifiers, modifiers);
            ClassicAssert.AreEqual(expectedKey, key);
        }
    }
}
