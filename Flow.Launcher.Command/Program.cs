using System.Diagnostics;

namespace Flow.Launcher.Command;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 0) return -1;

        // Start process with arguments
        // Usage: Flow.Launcher.Command -StartProcess -FileName <file> -WorkingDirectory <directory> -Arguments <args> -UseShellExecute <true|false> -Verb <verb>
        if (args[0] == @"-StartProcess")
        {
            var fileName = string.Empty;
            var workingDirectory = Environment.CurrentDirectory;
            var argumentList = new List<string>();
            var useShellExecute = true;
            var verb = string.Empty;
            var isArguments = false;

            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-FileName":
                        if (i + 1 < args.Length)
                            fileName = args[++i];
                        isArguments = false;
                        break;

                    case "-WorkingDirectory":
                        if (i + 1 < args.Length)
                            workingDirectory = args[++i];
                        isArguments = false;
                        break;

                    case "-Arguments":
                        if (i + 1 < args.Length)
                            argumentList.Add(args[++i]);
                        isArguments = true;
                        break;

                    case "-UseShellExecute":
                        if (i + 1 < args.Length && bool.TryParse(args[++i], out bool useShell))
                            useShellExecute = useShell;
                        isArguments = false;
                        break;

                    case "-Verb":
                        if (i + 1 < args.Length)
                            verb = args[++i];
                        isArguments = false;
                        break;

                    default:
                        if (isArguments)
                            argumentList.Add(args[i]);
                        else
                            Console.WriteLine($"Unknown parameter: {args[i]}");
                        break;
                }
            }

            if (string.IsNullOrEmpty(fileName))
            {
                Console.WriteLine("Error: -FileName is required.");
                return -2;
            }

            try
            {
                ProcessStartInfo info;
                if (argumentList.Count == 0)
                {
                    info = new ProcessStartInfo
                    {
                        FileName = fileName,
                        WorkingDirectory = workingDirectory,
                        UseShellExecute = useShellExecute,
                        Verb = verb
                    };
                }
                else if (argumentList.Count == 1)
                {
                    info = new ProcessStartInfo
                    {
                        FileName = fileName,
                        WorkingDirectory = workingDirectory,
                        Arguments = argumentList[0],
                        UseShellExecute = useShellExecute,
                        Verb = verb
                    };
                }
                else
                {
                    info = new ProcessStartInfo
                    {
                        FileName = fileName,
                        WorkingDirectory = workingDirectory,
                        UseShellExecute = useShellExecute,
                        Verb = verb
                    };
                    foreach (var arg in argumentList)
                    {
                        info.ArgumentList.Add(arg);
                    }
                }
                Process.Start(info)?.Dispose();
                Console.WriteLine("Success.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return -3;
            }
        }

        return -4;
    }
}
