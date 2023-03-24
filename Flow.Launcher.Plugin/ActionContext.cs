using System.Windows.Input;

namespace Flow.Launcher.Plugin
{
    public class ActionContext
    {
        public SpecialKeyState SpecialKeyState { get; set; }
    }

    public class SpecialKeyState
    {
        public bool CtrlPressed { get; set; }
        public bool ShiftPressed { get; set; }
        public bool AltPressed { get; set; }
        public bool WinPressed { get; set; }

        public ModifierKeys ToModifierKeys()
        {
            return (CtrlPressed ? ModifierKeys.Control : ModifierKeys.None) |
                   (ShiftPressed ? ModifierKeys.Shift : ModifierKeys.None) |
                   (AltPressed ? ModifierKeys.Alt : ModifierKeys.None) |
                   (WinPressed ? ModifierKeys.Windows : ModifierKeys.None);
        }
    }
}
