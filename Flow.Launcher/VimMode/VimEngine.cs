using System;

namespace Flow.Launcher.VimMode
{
    /// <summary>
    /// Represents the different states of the Vim engine.
    /// </summary>
    public enum VimModeType
    {
        Insert,
        Normal,
        Visual,
        VisualLine
    }

    /// <summary>
    /// Core state machine for managing Vim modes and transitions.
    /// </summary>
    public class VimEngine
    {
        /// <summary>
        /// Gets the current Vim mode.
        /// </summary>
        public VimModeType CurrentMode { get; private set; } = VimModeType.Insert;

        /// <summary>
        /// Event fired whenever the Vim mode changes.
        /// </summary>
        public event Action<VimModeType> ModeChanged;

        /// <summary>
        /// Switches the engine to Insert mode.
        /// </summary>
        public void SwitchToInsert()
        {
            var oldMode = CurrentMode;
            CurrentMode = VimModeType.Insert;
            if (oldMode != VimModeType.Insert)
                ModeChanged?.Invoke(CurrentMode);
        }

        /// <summary>
        /// Switches the engine to Normal mode.
        /// </summary>
        public void SwitchToNormal()
        {
            var oldMode = CurrentMode;
            CurrentMode = VimModeType.Normal;
            if (oldMode != VimModeType.Normal)
                ModeChanged?.Invoke(CurrentMode);
        }

        /// <summary>
        /// Switches the engine to Visual mode.
        /// </summary>
        public void SwitchToVisual()
        {
            var oldMode = CurrentMode;
            CurrentMode = VimModeType.Visual;
            if (oldMode != VimModeType.Visual)
                ModeChanged?.Invoke(CurrentMode);
        }

        /// <summary>
        /// Switches the engine to Visual Line mode.
        /// </summary>
        public void SwitchToVisualLine()
        {
            var oldMode = CurrentMode;
            CurrentMode = VimModeType.VisualLine;
            if (oldMode != VimModeType.VisualLine)
                ModeChanged?.Invoke(CurrentMode);
        }
    }
}
