using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using System;
using System.Collections.Generic;
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
            return string.Format(@" Process Name: {0} ,Process ID: {1} ,
                                    Port: {2} ,\nPath : {3}", ProcessName,
                                    ProcessID, Port, Path);
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
    ///
    /// </summary>
    internal class PortHelper
    {
        private const short MINIMUM_TOKEN_IN_A_LINE = 5;
        private const string COMMAND_EXE = "cmd";

        public PortHelper()
        {

        }

        public static Tuple<bool, PortDetail> GetPortDetails(int port)
        {
            PortDetail PortDetail = new PortDetail();
            Tuple<bool, PortDetail> result = Tuple.Create(false, PortDetail);

            // execute netstat command for the given port
            string commandArgument = string.Format("/c netstat -an -o -p tcp|findstr \":{0}.*LISTENING\"", port);

            string commandOut = ExecuteCommandAndCaptureOutput(COMMAND_EXE, commandArgument);
            if (string.IsNullOrEmpty(commandOut))
            {
                // port is not in use
                return result;
            }

            var stringTokens = commandOut.Split(default(Char[]), StringSplitOptions.RemoveEmptyEntries);
            if (stringTokens.Length < MINIMUM_TOKEN_IN_A_LINE)
            {
                return result;
            }

            // split host:port
            var hostPortTokens = stringTokens[1].Split(new char[] { ':' });
            if (hostPortTokens.Length < 2)
            {
                return result;
            }

            int portFromHostPortToken = 0;
            if (!int.TryParse(hostPortTokens[1], out portFromHostPortToken))
            {
                return result;
            }
            if (portFromHostPortToken != port)
            {
                return result;
            }

            PortDetail.Port = port;
            PortDetail.ProcessID = int.Parse(stringTokens[4].Trim());
            Tuple<string, string> processNameAndPath = null;
            try
            {
                processNameAndPath = GetProcessNameAndCommandLineArgs(PortDetail.ProcessID);
                PortDetail.ProcessName = processNameAndPath.Item1;
                PortDetail.Path = processNameAndPath.Item2;
                PortDetail.Process = Process.GetProcessById(PortDetail.ProcessID);
                result = Tuple.Create(true, PortDetail);
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.ToString());

            }

            return result;

        }
        /// <summary>
        /// Using WMI API to get process name and path instead of
        /// Process.GetProcessById, because if calling process ins
        /// 32 bit and given process id is 64 bit, caller will not be able to
        /// get the process name
        /// </summary>
        /// <param name="processID"></param>
        /// <returns></returns>
        private static Tuple<string, string> GetProcessNameAndCommandLineArgs(int processID)
        {
            Tuple<string, string> result = Tuple.Create(string.Empty, string.Empty);
            string query = string.Format("Select Name,ExecutablePath from Win32_Process WHERE ProcessId='{0}'", processID);
            try
            {
                ObjectQuery wql = new ObjectQuery(query);
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(wql);
                ManagementObjectCollection results = searcher.Get();

                // interested in first result.
                foreach (ManagementObject item in results)
                {
                    result = Tuple.Create<string, string>(Convert.ToString(item["Name"]),
                    Convert.ToString(item["ExecutablePath"]));
                    break;

                }
            }
            catch (Exception)
            {

                throw;
            }

            return result;

        }

        /// <summary>
        /// Execute the given command and captures the output
        /// </summary>
        /// <param name="commandName"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        private static string ExecuteCommandAndCaptureOutput(string commandName, string arguments)
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
                Log.Exception($"|ProcessKiller.CreateResultsFromProcesses|Failed to ExecuteCommandAndCaptureOutput {commandName + arguments}", exp);
                Console.WriteLine(exp.ToString());
            }
            return commandOut;
        }
    }
}
