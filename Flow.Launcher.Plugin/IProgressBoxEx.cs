namespace Flow.Launcher.Plugin;

/// <summary>
/// Interface for progress box
/// </summary>
public interface IProgressBoxEx
{
    /// <summary>
    /// Show progress box. It should be called from the main ui thread.
    /// </summary>
    /// <param name="progress">
    /// Progress value. Should be between 0 and 100. When progress is 100, the progress box will be closed.
    /// </param>
    public void ReportProgress(double progress);

    /// <summary>
    /// Close progress box. It should be called from the main ui thread.
    /// </summary>
    public void Close();
}
