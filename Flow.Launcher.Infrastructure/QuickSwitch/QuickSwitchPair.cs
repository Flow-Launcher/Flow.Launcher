using Flow.Launcher.Plugin;

namespace Flow.Launcher.Infrastructure.QuickSwitch;

public class QuickSwitchExplorerPair
{
    public IQuickSwitchExplorer Plugin { get; init; }

    public PluginMetadata Metadata { get; init; }

    public override string ToString()
    {
        return Metadata.Name;
    }

    public override bool Equals(object obj)
    {
        if (obj is QuickSwitchExplorerPair r)
        {
            return string.Equals(r.Metadata.ID, Metadata.ID);
        }
        else
        {
            return false;
        }
    }

    public override int GetHashCode()
    {
        var hashcode = Metadata.ID?.GetHashCode() ?? 0;
        return hashcode;
    }
}

public class QuickSwitchDialogPair
{
    public IQuickSwitchDialog Plugin { get; init; }

    public PluginMetadata Metadata { get; init; }

    public override string ToString()
    {
        return Metadata.Name;
    }

    public override bool Equals(object obj)
    {
        if (obj is QuickSwitchDialogPair r)
        {
            return string.Equals(r.Metadata.ID, Metadata.ID);
        }
        else
        {
            return false;
        }
    }

    public override int GetHashCode()
    {
        var hashcode = Metadata.ID?.GetHashCode() ?? 0;
        return hashcode;
    }
}
