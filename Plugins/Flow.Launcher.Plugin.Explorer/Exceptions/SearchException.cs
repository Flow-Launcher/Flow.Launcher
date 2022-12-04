using System;

namespace Flow.Launcher.Plugin.Explorer.Exceptions
{
    public class SearchException : Exception
    {
        public string EngineName { get; }
        public SearchException(string engineName, string message) : base(message)
        {
            EngineName = engineName;
        }

        public SearchException(string engineName, string message, Exception innerException) : base(message, innerException)
        {
            EngineName = engineName;
        }
        
        public override string ToString()
        {
            return $"{EngineName} Search Exception:\n {base.ToString()}";
        }
    }
}
