using System.Threading;

namespace Flow.Launcher.Plugin.Explorer.Search.Everything
{
    internal static class EverythingSdkLoader
    {
        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        public static void EnsureLoaded(string sdkDirectory, bool enableEverything15Support)
        {
            _semaphore.Wait();
            try
            {
                if (enableEverything15Support)
                {
                    if (!Everything3ApiDllImport.IsLoaded)
                        Everything3ApiDllImport.Load(sdkDirectory);
                }
                else
                {
                    if (!EverythingApiDllImport.IsLoaded)
                        EverythingApiDllImport.Load(sdkDirectory);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
