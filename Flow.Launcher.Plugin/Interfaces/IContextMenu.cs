using System.Collections.Generic;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Adds support for presenting additional options for a given <see cref="Result"/> from a context menu.
    /// </summary>
    public interface IContextMenu : IFeatures
    {
        /// <summary>
        /// Load context menu items for the given result.
        /// </summary>
        /// <param name="selectedResult">
        /// The <see cref="Result"/> for which the user has activated the context menu.
        /// </param>
        List<Result> LoadContextMenus(Result selectedResult);
    }
}