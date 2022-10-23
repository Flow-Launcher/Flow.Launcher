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
        public const string Python = "Python";

        /// <summary>
        /// C#
        /// </summary>
        public const string CSharp = "CSharp";

        /// <summary>
        /// F#
        /// </summary>
        public const string FSharp = "FSharp";

        /// <summary>
        /// Standard .exe
        /// </summary>
        public const string Executable = "Executable";

        /// <summary>
        /// TypeScript
        /// </summary>
        public const string TypeScript = "TypeScript";

        /// <summary>
        /// JavaScript
        /// </summary>
        public const string JavaScript = "JavaScript";

        /// <summary>
        /// Determines if this language is a .NET language
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
        public static bool IsDotNet(string language)
        {
            return language.Equals(CSharp, StringComparison.OrdinalIgnoreCase)
                || language.Equals(FSharp, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines if this language is supported
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
        public static bool IsAllowed(string language)
        {
            return IsDotNet(language)
                || language.Equals(Python, StringComparison.OrdinalIgnoreCase)
                || language.Equals(Executable, StringComparison.OrdinalIgnoreCase)
                || language.Equals(TypeScript, StringComparison.OrdinalIgnoreCase)
                || language.Equals(JavaScript, StringComparison.OrdinalIgnoreCase);
        }
    }
}
