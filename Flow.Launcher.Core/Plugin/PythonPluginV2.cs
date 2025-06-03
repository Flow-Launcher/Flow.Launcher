using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Flow.Launcher.Core.Plugin.JsonRPCV2Models;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin;
using Microsoft.VisualStudio.Threading;
using Nerdbank.Streams;
using StreamJsonRpc;

namespace Flow.Launcher.Core.Plugin
{
    internal sealed class PythonPluginV2 : ProcessStreamPluginV2
    {
        protected override ProcessStartInfo StartInfo { get; set; }

        public PythonPluginV2(string filename)
        {
            StartInfo = new ProcessStartInfo { FileName = filename, };

            var path = Path.Combine(Constant.ProgramDirectory, JsonRpc);
            StartInfo.EnvironmentVariables["PYTHONPATH"] = path;
            StartInfo.EnvironmentVariables["PYTHONDONTWRITEBYTECODE"] = "1";
        }

        public override async Task InitAsync(PluginInitContext context)
        {
            // Run .py files via `-c <code>`
            if (context.CurrentPluginMetadata.ExecuteFilePath.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
            {
                var rootDirectory = context.CurrentPluginMetadata.PluginDirectory;
                var libDirectory = Path.Combine(rootDirectory, "lib");
                var libPyWin32Directory = Path.Combine(libDirectory, "win32");
                var libPyWin32LibDirectory = Path.Combine(libPyWin32Directory, "lib");
                var pluginDirectory = Path.Combine(rootDirectory, "plugin");
                var filePath = context.CurrentPluginMetadata.ExecuteFilePath;

                // This makes it easier for plugin authors to import their own modules.
                // They won't have to add `.`, `./lib`, or `./plugin` to their sys.path manually.
                // Instead of running the .py file directly, we pass the code we want to run as a CLI argument.
                // This code sets sys.path for the plugin author and then runs the .py file via runpy.
                StartInfo.ArgumentList.Add("-c");
                StartInfo.ArgumentList.Add(
                    $"""
                     import sys
                     sys.path.append(r'{rootDirectory}')
                     sys.path.append(r'{libDirectory}')
                     sys.path.append(r'{libPyWin32LibDirectory}')
                     sys.path.append(r'{libPyWin32Directory}')
                     sys.path.append(r'{pluginDirectory}')
                     
                     import runpy
                     runpy.run_path(r'{filePath}', None, '__main__')
                     """
                );
            }
            // Run .pyz files as is
            else
            {
                StartInfo.ArgumentList.Add(context.CurrentPluginMetadata.ExecuteFilePath);
            }
            await base.InitAsync(context);
        }

        protected override MessageHandlerType MessageHandler { get; } = MessageHandlerType.NewLineDelimited;
    }
}
