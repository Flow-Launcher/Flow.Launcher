using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Core.Plugin;

internal class PythonPluginV2 : JsonRPCPlugin2
{
    private readonly ProcessStartInfo _startInfo;
    public override string SupportedLanguage { get; set; } = AllowedLanguage.Python;

    private Process process;


    public PythonPluginV2(string filename)
    {
        _startInfo = new ProcessStartInfo
        {
            FileName = filename,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true
        };

        // temp fix for issue #667
        var path = Path.Combine(Constant.ProgramDirectory, JsonRPC);
        _startInfo.EnvironmentVariables["PYTHONPATH"] = path;

        _startInfo.EnvironmentVariables["FLOW_VERSION"] = Constant.Version;
        _startInfo.EnvironmentVariables["FLOW_PROGRAM_DIRECTORY"] = Constant.ProgramDirectory;
        _startInfo.EnvironmentVariables["FLOW_APPLICATION_DIRECTORY"] = Constant.ApplicationDirectory;

        //Add -B flag to tell python don't write .py[co] files. Because .pyc contains location infos which will prevent python portable
        _startInfo.ArgumentList.Add("-B");
    }

    protected override Task InitializeAsync(PluginInitContext context)
    {
        _startInfo.ArgumentList.Add(context.CurrentPluginMetadata.ExecuteFilePath);
        _startInfo.WorkingDirectory = context.CurrentPluginMetadata.PluginDirectory;
        process = new Process();
        process.StartInfo = _startInfo;
        process.Start();

        InputStream = process.StandardInput.BaseStream;
        OutputStream = process.StandardOutput.BaseStream;
        process.StandardError.ReadToEndAsync().ContinueWith((t) =>
        {
            if (!string.IsNullOrEmpty(t.Result))
            {
                LogErrorMessage(t.Result);
            }
        });

        return Task.CompletedTask;
    }

}