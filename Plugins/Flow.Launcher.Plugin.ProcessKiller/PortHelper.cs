using System;
using System.Diagnostics;
using System.Linq;
using System.Management;

namespace Flow.Launcher.Plugin.ProcessKiller
{
    internal class PortDetail
    {
        public int Port { get; set; }
        public int ProcessID { get; set; }
        public string ProcessName { get; set; }
        public Process Process { get; set; }
        public string Path { get; set; }

        public override string ToString()
        {
            return $@" Process Name: {ProcessName}, Process ID: {ProcessID}, Port: {Port}, Path : {Path}";
        }
    }

    /// <summary>
    /// Usage:
    /// int port = 8081
    /// TcpHelperUtil tcpHelper = new TcpHelperUtil();
    /// var details = tcpHelper.GetPortDetails(port);
    /// if (details.Item1)
    /// {
    ///     Console.WriteLine("Port {0} in Use",port);
    ///     Console.WriteLine(details.Item2.ToString());
    /// }else
    /// {
    ///     Console.WriteLine("Port {0} is free ",port);
    /// }
    /// </summary>
    internal class PortHelper
    {
        private const short MINIMUM_TOKEN_IN_A_LINE = 5;
        private const string COMMAND_EXE = "cmd";

        private static string ClassName => nameof(PortHelper);

        public static (bool Result, PortDetail Detail) GetPortDetails(int port, PluginInitContext context)
        {
            var portDetail = new PortDetail();

            // execute netstat command for the given port
            string commandArgument = string.Format("/c netstat -an -o -p tcp|findstr \":{0}.*LISTENING\"", port);

            string commandOut = ExecuteCommandAndCaptureOutput(COMMAND_EXE, commandArgument, context);
            if (string.IsNullOrEmpty(commandOut))
            {
                // port is not in use
                return (false, portDetail);
            }

            var stringTokens = commandOut.Split(default(char[]), StringSplitOptions.RemoveEmptyEntries);
            if (stringTokens.Length < MINIMUM_TOKEN_IN_A_LINE)
            {
                return (false, portDetail);
            }

            // split host:port
            var hostPortTokens = stringTokens[1].Split(new char[] { ':' });
            if (hostPortTokens.Length < 2)
            {
                return (false, portDetail);
            }

            if (!int.TryParse(hostPortTokens[1], out var portFromHostPortToken))
            {
                return (false, portDetail);
            }

            if (portFromHostPortToken != port)
            {
                return (false, portDetail);
            }

            portDetail.Port = port;
            portDetail.ProcessID = int.Parse(stringTokens[4].Trim());
            (string Name, string Path) processNameAndPath;
            try
            {
                processNameAndPath = GetProcessNameAndCommandLineArgs(portDetail.ProcessID, context);
                portDetail.ProcessName = processNameAndPath.Name;
                portDetail.Path = processNameAndPath.Path;
                portDetail.Process = Process.GetProcessById(portDetail.ProcessID);
                return (true, portDetail);
            }
            catch (Exception e)
            {
                context.API.LogException(ClassName, "Failed to get process name and path", e);
            }

            return (false, portDetail);
        }

        /// <summary>
        /// Using WMI API to get process name and path instead of
        /// Process.GetProcessById, because if calling process ins
        /// 32 bit and given process id is 64 bit, caller will not be able to
        /// get the process name
        /// </summary>
        /// <param name="processID"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        private static (string Name, string Path) GetProcessNameAndCommandLineArgs(int processID, PluginInitContext context)
        {
            var name = string.Empty;
            var path = string.Empty;
            string query = string.Format("Select Name,ExecutablePath from Win32_Process WHERE ProcessId='{0}'", processID);
            try
            {
                ObjectQuery wql = new ObjectQuery(query);
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(wql);
                ManagementObjectCollection results = searcher.Get();

                // interested in first result.
                foreach (var item in results.Cast<ManagementObject>())
                {
                    name = Convert.ToString(item["Name"]);
                    path = Convert.ToString(item["ExecutablePath"]);
                    break;
                }
            }
            catch (Exception e)
            {
                context.API.LogException(ClassName, "Failed to get process name and path", e);
            }

            return (name, path);
        }

        /// <summary>
        /// Execute the given command and captures the output
        /// </summary>
        /// <param name="commandName"></param>
        /// <param name="arguments"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        private static string ExecuteCommandAndCaptureOutput(string commandName, string arguments, PluginInitContext context)
        {
            string commandOut = string.Empty;
            Process process = new Process();
            process.StartInfo.FileName = commandName;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();

            commandOut = process.StandardOutput.ReadToEnd();
            string errors = process.StandardError.ReadToEnd();
            try
            {
                process.WaitForExit(TimeSpan.FromSeconds(2).Milliseconds);
            }
            catch (Exception exp)
            {
                context.API.LogException(ClassName, $"Failed to ExecuteCommandAndCaptureOutput {commandName + arguments}", exp);
            }

            return commandOut;
        }
    }
}
