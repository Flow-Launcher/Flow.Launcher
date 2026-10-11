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

        private void SetMode(VimModeType newMode)
        {
            var oldMode = CurrentMode;
            CurrentMode = newMode;
            if (oldMode != newMode)
                ModeChanged?.Invoke(CurrentMode);
        }

        /// <summary>
        /// Switches the engine to Insert mode.
        /// </summary>
        public void SwitchToInsert() => SetMode(VimModeType.Insert);

        /// <summary>
        /// Switches the engine to Normal mode.
        /// </summary>
        public void SwitchToNormal() => SetMode(VimModeType.Normal);

        /// <summary>
        /// Switches the engine to Visual mode.
        /// </summary>
        public void SwitchToVisual() => SetMode(VimModeType.Visual);

        /// <summary>
        /// Switches the engine to Visual Line mode.
        /// </summary>
        public void SwitchToVisualLine() => SetMode(VimModeType.VisualLine);
    }
}
