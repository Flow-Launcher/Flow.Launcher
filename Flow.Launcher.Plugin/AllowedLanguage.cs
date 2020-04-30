namespace Flow.Launcher.Plugin
{
    public static class AllowedLanguage
    {
        public static string Python
        {
            get { return "PYTHON"; }
        }

        public static string CSharp
        {
            get { return "CSHARP"; }
        }

        public static string FSharp
        {
            get { return "FSHARP"; }
        }

        public static string Executable
        {
            get { return "EXECUTABLE"; }
        }

        public static bool IsDotNet(string language)
        {
            return language.ToUpper() == CSharp
                || language.ToUpper() == FSharp;
        }

        public static bool IsAllowed(string language)
        {
            return IsDotNet(language)
                || language.ToUpper() == Python.ToUpper()
                || language.ToUpper() == Executable.ToUpper();
        }
    }
}