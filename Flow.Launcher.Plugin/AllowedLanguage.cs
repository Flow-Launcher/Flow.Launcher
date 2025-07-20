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
        /// Python V2
        /// </summary>
        public const string PythonV2 = "Python_v2";

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
        /// Standard .exe
        /// </summary>
        public const string ExecutableV2 = "Executable_V2";

        /// <summary>
        /// TypeScript
        /// </summary>
        public const string TypeScript = "TypeScript";

        /// <summary>
        /// TypeScript
        /// </summary>
        public const string TypeScriptV2 = "TypeScript_V2";

        /// <summary>
        /// JavaScript
        /// </summary>
        public const string JavaScript = "JavaScript";

        /// <summary>
        /// JavaScript
        /// </summary>
        public const string JavaScriptV2 = "JavaScript_V2";

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
        /// Determines if this language is a Python language
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
        public static bool IsPython(string language)
        {
            return language.Equals(Python, StringComparison.OrdinalIgnoreCase)
                || language.Equals(PythonV2, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines if this language is a Node.js language
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
        public static bool IsNodeJs(string language)
        {
            return language.Equals(TypeScript, StringComparison.OrdinalIgnoreCase)
                || language.Equals(TypeScriptV2, StringComparison.OrdinalIgnoreCase)
                || language.Equals(JavaScript, StringComparison.OrdinalIgnoreCase)
                || language.Equals(JavaScriptV2, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines if this language is a executable language
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
        public static bool IsExecutable(string language)
        {
            return language.Equals(Executable, StringComparison.OrdinalIgnoreCase)
                || language.Equals(ExecutableV2, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines if this language is supported
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
        public static bool IsAllowed(string language)
        {
            return IsDotNet(language)
                || IsPython(language)
                || IsNodeJs(language)
                || IsExecutable(language);
        }
    }
}
