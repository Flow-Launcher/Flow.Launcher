using System;

namespace Flow.Launcher.VimMode
{
    /// <summary>
    /// Represents the different states of the Vim engine.
    /// </summary>
    public enum VimModes
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
        public VimModes CurrentMode { get; private set; } = VimModes.Insert;

        /// <summary>
        /// Event fired whenever the Vim mode changes.
        /// </summary>
        public event Action<VimModes> ModeChanged;

        /// <summary>
        /// Switches the engine to Insert mode.
        /// </summary>
        public void SwitchToInsert()
        {
            var oldMode = CurrentMode;
            CurrentMode = VimModes.Insert;
            if (oldMode != VimModes.Insert)
                ModeChanged?.Invoke(CurrentMode);
        }

        /// <summary>
        /// Switches the engine to Normal mode.
        /// </summary>
        public void SwitchToNormal()
        {
            var oldMode = CurrentMode;
            CurrentMode = VimModes.Normal;
            if (oldMode != VimModes.Normal)
                ModeChanged?.Invoke(CurrentMode);
        }

        /// <summary>
        /// Switches the engine to Visual mode.
        /// </summary>
        public void SwitchToVisual()
        {
            var oldMode = CurrentMode;
            CurrentMode = VimModes.Visual;
            if (oldMode != VimModes.Visual)
                ModeChanged?.Invoke(CurrentMode);
        }

        /// <summary>
        /// Switches the engine to Visual Line mode.
        /// </summary>
        public void SwitchToVisualLine()
        {
            var oldMode = CurrentMode;
            CurrentMode = VimModes.VisualLine;
            if (oldMode != VimModes.VisualLine)
                ModeChanged?.Invoke(CurrentMode);
        }
    }
}
