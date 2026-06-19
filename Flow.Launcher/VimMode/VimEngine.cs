using System;

namespace Flow.Launcher.VimMode
{
    public enum VimModes
    {
        Insert,
        Normal,
        Visual,
        VisualLine
    }

    public class VimEngine
    {
        public VimModes CurrentMode { get; private set; } = VimModes.Insert;

        public event Action<VimModes> ModeChanged;

        public void SwitchToInsert()
        {
            var oldMode = CurrentMode;
            CurrentMode = VimModes.Insert;
            if (oldMode != VimModes.Insert)
                ModeChanged?.Invoke(CurrentMode);
        }

        public void SwitchToNormal()
        {
            var oldMode = CurrentMode;
            CurrentMode = VimModes.Normal;
            if (oldMode != VimModes.Normal)
                ModeChanged?.Invoke(CurrentMode);
        }

        public void SwitchToVisual()
        {
            var oldMode = CurrentMode;
            CurrentMode = VimModes.Visual;
            if (oldMode != VimModes.Visual)
                ModeChanged?.Invoke(CurrentMode);
        }

        public void SwitchToVisualLine()
        {
            var oldMode = CurrentMode;
            CurrentMode = VimModes.VisualLine;
            if (oldMode != VimModes.VisualLine)
                ModeChanged?.Invoke(CurrentMode);
        }
    }
}
