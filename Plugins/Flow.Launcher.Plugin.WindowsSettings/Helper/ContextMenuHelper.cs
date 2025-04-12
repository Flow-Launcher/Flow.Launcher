using System;
using System.Collections.Generic;
using System.Windows;

namespace Flow.Launcher.Plugin.WindowsSettings.Helper
{
    /// <summary>
    /// Helper class to easier work with context menu entries
    /// </summary>
    internal static class ContextMenuHelper
    {
        /// <summary>
        /// Return a list with all context menu entries for the given <see cref="Result"/>
        /// <para>Symbols taken from <see href="https://docs.microsoft.com/en-us/windows/uwp/design/style/segoe-ui-symbol-font"/></para>
        /// </summary>
        /// <param name="result">The result for the context menu entires</param>
        /// <param name="assemblyName">The name of the this assembly</param>
        /// <returns>A list with context menu entries</returns>
        internal static List<Result> GetContextMenu(in Result result, in string assemblyName)
        {
            return new List<Result>(0);
        }        
    }
}
