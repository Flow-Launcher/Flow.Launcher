using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading.Tasks;

namespace Flow.Launcher.Core.Plugin
{
    internal class PluginAssemblyLoader : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver dependencyResolver;

        private readonly AssemblyName assemblyName;

        internal PluginAssemblyLoader(string assemblyFilePath)
            : base(name: Path.GetFileNameWithoutExtension(assemblyFilePath), isCollectible: true)
        {
            dependencyResolver = new AssemblyDependencyResolver(assemblyFilePath);
            assemblyName = new AssemblyName(Path.GetFileNameWithoutExtension(assemblyFilePath));
        }

        /// <summary>
        /// Initiates unload of the given load context and returns a weak reference that can be used to
        /// verify the context has actually been collected. Kept in a separate non-inlined method so the
        /// caller's stack frame holds no strong reference to the context that would prevent collection.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static WeakReference UnloadAndGetWeakReference(PluginAssemblyLoader loader)
        {
            var weakReference = new WeakReference(loader);
            loader.Unload();
            return weakReference;
        }

        /// <summary>
        /// Waits for an unloaded context to be collected. Returns false if the context is still alive
        /// after all attempts, which means something (e.g. a cached delegate) is pinning the old assembly.
        /// </summary>
        internal static async Task<bool> WaitForUnloadAsync(WeakReference weakReference, int maxAttempts = 10)
        {
            for (var i = 0; i < maxAttempts && weakReference.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                if (!weakReference.IsAlive) return true;
                // ConfigureAwait(false) keeps the blocking GC loop off the UI thread's context
                await Task.Delay(100).ConfigureAwait(false);
            }

            return !weakReference.IsAlive;
        }

        internal Assembly LoadAssemblyAndDependencies()
        {
            return LoadFromAssemblyName(assemblyName);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            string assemblyPath = dependencyResolver.ResolveAssemblyToPath(assemblyName);

            // When resolving dependencies, ignore assembly depenedencies that already exits with Flow.Launcher
            // Otherwise duplicate assembly will be loaded and some weird behavior will occur, such as WinRT.Runtime.dll
            // will fail due to loading multiple versions in process, each with their own static instance of registration state
            var existAssembly = Default.Assemblies.FirstOrDefault(x => x.FullName == assemblyName.FullName);

            return existAssembly ?? (assemblyPath == null ? null : LoadFromAssemblyPath(assemblyPath));
        }
        
        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = dependencyResolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (!string.IsNullOrEmpty(path))
            {
                return LoadUnmanagedDllFromPath(path);
            }

            return IntPtr.Zero;
        }

        internal Type FromAssemblyGetTypeOfInterface(Assembly assembly, Type type)
        {
            var allTypes = assembly.ExportedTypes;
            return allTypes.First(o => o.IsClass && !o.IsAbstract && o.GetInterfaces().Any(t => t == type));
        }
    }
}
