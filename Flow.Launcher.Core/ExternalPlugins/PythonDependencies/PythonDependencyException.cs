#nullable enable

using System;

namespace Flow.Launcher.Core.ExternalPlugins.PythonDependencies
{
    public sealed class PythonDependencyException : Exception
    {
        public PythonDependencyException(string message, Exception? inner = null) : base(message, inner)
        {
        }
    }
}
