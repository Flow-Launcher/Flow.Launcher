﻿using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

// http://blogs.microsoft.co.il/arik/2010/05/28/wpf-single-instance-application/
// modified to allow single instace restart
namespace Flow.Launcher.Helper
{
    public interface ISingleInstanceApp
    {
        void OnSecondAppStarted();
    }

    /// <summary>
    /// This class checks to make sure that only one instance of 
    /// this application is running at a time.
    /// </summary>
    /// <remarks>
    /// Note: this class should be used with some caution, because it does no
    /// security checking. For example, if one instance of an app that uses this class
    /// is running as Administrator, any other instance, even if it is not
    /// running as Administrator, can activate it with command line arguments.
    /// For most apps, this will not be much of an issue.
    /// </remarks>
    public static class SingleInstance<TApplication> where TApplication : Application, ISingleInstanceApp
    {
        #region Private Fields

        /// <summary>
        /// String delimiter used in channel names.
        /// </summary>
        private const string Delimiter = ":";

        /// <summary>
        /// Suffix to the channel name.
        /// </summary>
        private const string ChannelNameSuffix = "SingeInstanceIPCChannel";
        private const string InstanceMutexName = "Flow.Launcher_Unique_Application_Mutex";

        /// <summary>
        /// Application mutex.
        /// </summary>
        internal static Mutex SingleInstanceMutex { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Checks if the instance of the application attempting to start is the first instance. 
        /// If not, activates the first instance.
        /// </summary>
        /// <returns>True if this is the first instance of the application.</returns>
        public static bool InitializeAsFirstInstance()
        {
            // Build unique application Id and the IPC channel name.
            string applicationIdentifier = InstanceMutexName + Environment.UserName;

            string channelName = string.Concat(applicationIdentifier, Delimiter, ChannelNameSuffix);

            // Create mutex based on unique application Id to check if this is the first instance of the application. 
            SingleInstanceMutex = new Mutex(true, applicationIdentifier, out var firstInstance);
            if (firstInstance)
            {
                _ = CreateRemoteServiceAsync(channelName);
                return true;
            }
            else
            {
                _ = SignalFirstInstanceAsync(channelName);
                return false;
            }
        }

        /// <summary>
        /// Cleans up single-instance code, clearing shared resources, mutexes, etc.
        /// </summary>
        public static void Cleanup()
        {
            SingleInstanceMutex?.ReleaseMutex();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Creates a remote server pipe for communication. 
        /// Once receives signal from client, will activate first instance.
        /// </summary>
        /// <param name="channelName">Application's IPC channel name.</param>
        private static async Task CreateRemoteServiceAsync(string channelName)
        {
            using NamedPipeServerStream pipeServer = new NamedPipeServerStream(channelName, PipeDirection.In);
            while (true)
            {
                // Wait for connection to the pipe
                await pipeServer.WaitForConnectionAsync();

                // Do an asynchronous call to ActivateFirstInstance function
                Application.Current?.Dispatcher.Invoke(ActivateFirstInstance);

                // Disconect client
                pipeServer.Disconnect();
            }
        }

        /// <summary>
        /// Creates a client pipe and sends a signal to server to launch first instance
        /// </summary>
        /// <param name="channelName">Application's IPC channel name.</param>
        /// <param name="args">
        /// Command line arguments for the second instance, passed to the first instance to take appropriate action.
        /// </param>
        private static async Task SignalFirstInstanceAsync(string channelName)
        {
            // Create a client pipe connected to server
            using NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", channelName, PipeDirection.Out);

            // Connect to the available pipe
            await pipeClient.ConnectAsync(0);
        }

        /// <summary>
        /// Activates the first instance of the application with arguments from a second instance.
        /// </summary>
        /// <param name="args">List of arguments to supply the first instance of the application.</param>
        private static void ActivateFirstInstance()
        {
            // Set main window state and process command line args
            if (Application.Current == null)
            {
                return;
            }

            ((TApplication)Application.Current).OnSecondAppStarted();
        }

        #endregion
    }
}
