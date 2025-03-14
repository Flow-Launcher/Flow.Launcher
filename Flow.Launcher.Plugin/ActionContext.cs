using System.Windows.Input;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Context provided as a parameter when invoking a
    /// <see cref="Result.Action"/> or <see cref="Result.AsyncAction"/>
    /// </summary>
    public class ActionContext
    {
        /// <summary>
        /// Contains the press state of certain special keys.
        /// </summary>
        public SpecialKeyState SpecialKeyState { get; set; }
    }

    /// <summary>
    /// Contains the press state of certain special keys.
    /// </summary>
    public class SpecialKeyState
    {
        /// <summary>
        /// True if the Ctrl key is pressed.
        /// </summary>
        public bool CtrlPressed { get; set; }

        /// <summary>
        /// True if the Shift key is pressed.
        /// </summary>
        public bool ShiftPressed { get; set; }

        /// <summary>
        /// True if the Alt key is pressed.
        /// </summary>
        public bool AltPressed { get; set; }

        /// <summary>
        /// True if the Windows key is pressed.
        /// </summary>
        public bool WinPressed { get; set; }

        /// <summary>
        /// Get this object represented as a <see cref="ModifierKeys"/> flag combination.
        /// </summary>
        /// <returns></returns>
        public ModifierKeys ToModifierKeys()
        {
            return (CtrlPressed ? ModifierKeys.Control : ModifierKeys.None) |
                   (ShiftPressed ? ModifierKeys.Shift : ModifierKeys.None) |
                   (AltPressed ? ModifierKeys.Alt : ModifierKeys.None) |
                   (WinPressed ? ModifierKeys.Windows : ModifierKeys.None);
        }

        public static readonly SpecialKeyState Default = new () {
            CtrlPressed = false,
            ShiftPressed = false,
            AltPressed = false,
            WinPressed = false
        };
    }
}
