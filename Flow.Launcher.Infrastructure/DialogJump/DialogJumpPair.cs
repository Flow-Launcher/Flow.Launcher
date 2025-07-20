using Flow.Launcher.Plugin;

namespace Flow.Launcher.Infrastructure.DialogJump;

public class DialogJumpExplorerPair
{
    public IDialogJumpExplorer Plugin { get; init; }

    public PluginMetadata Metadata { get; init; }

    public override string ToString()
    {
        return Metadata.Name;
    }

    public override bool Equals(object obj)
    {
        if (obj is DialogJumpExplorerPair r)
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

public class DialogJumpDialogPair
{
    public IDialogJumpDialog Plugin { get; init; }

    public PluginMetadata Metadata { get; init; }

    public override string ToString()
    {
        return Metadata.Name;
    }

    public override bool Equals(object obj)
    {
        if (obj is DialogJumpDialogPair r)
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
