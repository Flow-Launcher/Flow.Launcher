using System;
using System.ComponentModel;
using System.Windows.Input;

namespace Flow.Launcher.Infrastructure.Hotkey
{
    public partial record struct HotkeyModel
    {
        private partial bool IsValidKeyGesture()
        {
            try
            {
                KeyGesture keyGesture = new KeyGesture(CharKey, ModifierKeys);
            }
            catch (System.Exception e) when
                (e is NotSupportedException || e is InvalidEnumArgumentException)
            {
                return false;
            }

            return true;
        }
    }
}
