using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
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
        /// Whether a delegate runs code from, or is bound to an object from, the given load context.
        /// </summary>
        internal static bool BelongsTo(Delegate handler, AssemblyLoadContext loadContext)
        {
            return GetLoadContext(handler.Method.Module.Assembly) == loadContext ||
                (handler.Target != null && GetLoadContext(handler.Target.GetType().Assembly) == loadContext);
        }

        /// <summary>
        /// Drops System.Text.Json's metadata caches. They hold every type that has been serialized, such as
        /// plugin settings types, and keep their load context alive even after the storage is removed.
        /// The runtime has no public API for this, so the cache-clearing handler that System.Text.Json
        /// registers for .NET hot reload is invoked instead.
        /// </summary>
        internal static void ClearJsonSerializerCaches()
        {
            try
            {
                var clearCacheMethods = typeof(JsonSerializer).Assembly
                    .GetCustomAttributes<MetadataUpdateHandlerAttribute>()
                    .Select(attribute => attribute.HandlerType.GetMethod("ClearCache",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, [typeof(Type[])]))
                    .Where(method => method != null);

                foreach (var clearCache in clearCacheMethods)
                {
                    clearCache.Invoke(null, [null]);
                }
            }
            catch (Exception e)
            {
                PublicApi.Instance.LogException(nameof(PluginAssemblyLoader), "Failed to clear System.Text.Json caches", e);
            }
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
