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
        public static string Python
        {
            get { return "PYTHON"; }
        }

        /// <summary>
        /// C#
        /// </summary>
        public static string CSharp
        {
            get { return "CSHARP"; }
        }

        /// <summary>
        /// F#
        /// </summary>
        public static string FSharp
        {
            get { return "FSHARP"; }
        }

        /// <summary>
        /// Standard .exe
        /// </summary>
        public static string Executable
        {
            get { return "EXECUTABLE"; }
        }

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
                || language.ToUpper() == Python.ToUpper()
                || language.ToUpper() == Executable.ToUpper();
        }
    }
}
