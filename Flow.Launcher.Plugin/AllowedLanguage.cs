using System;

namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Allowed plugin languages
    /// </summary>
    public static class AllowedLanguage
    {
        /// <summary>
        /// Python
        /// </summary>
        public const string Python = "PYTHON";
        
        /// <summary>
        /// Python V2
        /// </summary>
        public const string PythonV2 = "PYTHON_V2";

        /// <summary>
        /// C#
        /// </summary>
        public const string CSharp = "CSHARP";

        /// <summary>
        /// F#
        /// </summary>
        public const string FSharp = "FSHARP";

        /// <summary>
        /// Standard .exe
        /// </summary>
        public const string Executable = "EXECUTABLE";

        /// <summary>
        /// Determines if this language is a .NET language
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
        public static bool IsDotNet(string language)
        {
            return language.ToUpper() == CSharp
                || language.ToUpper() == FSharp;
        }

        /// <summary>
        /// Determines if this language is supported
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
        public static bool IsAllowed(string language)
        {
            return IsDotNet(language)
                || String.Equals(language, Python, StringComparison.CurrentCultureIgnoreCase)
                || String.Equals(language, PythonV2, StringComparison.CurrentCultureIgnoreCase)
                || String.Equals(language, Executable, StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
