using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Flow.Launcher.Infrastructure.Logger;

// http://blogs.microsoft.co.il/arik/2010/05/28/wpf-single-instance-application/
// modified to allow single instace restart
namespace Flow.Launcher.Helper
{
    public interface ISingleInstanceApp
    {
        void OnSecondAppStarted(string payload);
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
        public static bool InitializeAsFirstInstance(string args = null)
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
                try
                {
                    // Block until the signal and deep link payload are delivered,
                    // because the second instance exits right after this returns.
                    // Budget beyond the 3s connect timeout below so a slow connect still
                    // leaves room for the write to complete.
                    SignalFirstInstanceAsync(channelName, args).Wait(TimeSpan.FromSeconds(5));
                }
                catch
                {
                    // If the first instance cannot be reached there is nothing more to do
                }
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

                string payload = null;
                try
                {
                    // Guard against a client that connects but never writes or closes; cancelling the
                    // read (rather than abandoning it) avoids it faulting later against the disposed reader
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    using var reader = new StreamReader(pipeServer, Encoding.UTF8, false, 1024, leaveOpen: true);
                    payload = await reader.ReadLineAsync(cts.Token); // null when the client wrote nothing (plain activation)
                }
                catch (OperationCanceledException)
                {
                    // Client connected but never wrote/closed before the timeout; treat as a plain activation
                }
                catch (Exception e)
                {
                    // Never let a genuine pipe read failure kill the server loop, but still surface it
                    Log.Exception("SingleInstance", "Failed to read deep link payload from second instance", e);
                }

                // Do an asynchronous call to ActivateFirstInstance function so a deep-link handler
                // showing modal prompts cannot block this pipe accept loop and drop later activations
                var activation = Application.Current?.Dispatcher.InvokeAsync(() => ActivateFirstInstance(payload));
                activation?.Task.ContinueWith(
                    t => Log.Exception("SingleInstance", "Failed to activate first instance from second app", t.Exception),
                    TaskContinuationOptions.OnlyOnFaulted);

                // Disconect client
                pipeServer.Disconnect();
            }
        }

        /// <summary>
        /// Creates a client pipe and sends a signal to server to launch first instance
        /// </summary>
        /// <param name="channelName">Application's IPC channel name.</param>
        /// <param name="args">
        /// The deep link payload from the second instance, passed to the first instance to take appropriate action.
        /// </param>
        private static async Task SignalFirstInstanceAsync(string channelName, string args)
        {
            // Create a client pipe connected to server
            using NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", channelName, PipeDirection.Out);

            // Connect to the available pipe. Longer than the server's 2s read timeout so a stalled
            // prior client can't starve this connection attempt.
            await pipeClient.ConnectAsync(3000);

            // Send the deep link payload to the first instance if there is one
            if (!string.IsNullOrEmpty(args))
            {
                using var writer = new StreamWriter(pipeClient, Encoding.UTF8) { AutoFlush = true };
                await writer.WriteLineAsync(args);
            }
        }

        /// <summary>
        /// Activates the first instance of the application with the deep link payload from a second instance.
        /// </summary>
        /// <param name="payload">The deep link payload to supply the first instance of the application.</param>
        private static void ActivateFirstInstance(string payload)
        {
            // Set main window state and process the deep link payload
            if (Application.Current == null)
            {
                return;
            }

            ((TApplication)Application.Current).OnSecondAppStarted(payload);
        }

        #endregion
    }
}
