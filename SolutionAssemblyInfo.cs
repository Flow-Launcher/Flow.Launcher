using System.Reflection;
using System.Runtime.InteropServices;

#if DEBUG

[assembly: AssemblyConfiguration("Debug")]
[assembly: AssemblyDescription("Debug build, https://github.com/jjw24/Flow.Launcher")]
#else
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyDescription("Release build, https://github.com/jjw24/Flow.Launcher")]
#endif

[assembly: AssemblyCompany("Flow.Launcher")]
[assembly: AssemblyProduct("Flow.Launcher")]
[assembly: AssemblyCopyright("The MIT License (MIT)")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("2.0.0")]
[assembly: AssemblyFileVersion("2.0.0")]
[assembly: AssemblyInformationalVersion("2.0.0")]