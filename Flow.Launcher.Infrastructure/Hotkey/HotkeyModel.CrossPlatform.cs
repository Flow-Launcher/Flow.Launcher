namespace Flow.Launcher.Infrastructure.Hotkey
{
    public partial record struct HotkeyModel
    {
        // WPF KeyGesture does not exist here; only the modifier/printable-key rules in Validate apply.
        private partial bool IsValidKeyGesture() => true;
    }
}
