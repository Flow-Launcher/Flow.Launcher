using System;

namespace Flow.Launcher.Plugin
{
    public static class AllowedLanguage
    {
        public const string Python = "PYTHON";
        public const string PythonV2 = "PYTHON_V2";

        public const string CSharp = "CSHARP";

        public const string FSharp = "FSHARP";

        public const string Executable = "EXECUTABLE";

        public static bool IsDotNet(string language)
        {
            return language.ToUpper() == CSharp
                || language.ToUpper() == FSharp;
        }

        public static bool IsAllowed(string language)
        {
            return IsDotNet(language)
                || IsPython(language)
                || string.Equals(language, Executable, StringComparison.CurrentCultureIgnoreCase);
        }

        public static bool IsPython(string language)
        {
            return language.ToUpper() is Python or PythonV2;
        }
    }
}