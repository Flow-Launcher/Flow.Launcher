using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Flow.Launcher.Core.Plugin
{
    internal class PluginAssemblyLoader : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver dependencyResolver;

        private readonly AssemblyName assemblyName;

        internal PluginAssemblyLoader(string assemblyFilePath)
        {
            dependencyResolver = new AssemblyDependencyResolver(assemblyFilePath);
            assemblyName = new AssemblyName(Path.GetFileNameWithoutExtension(assemblyFilePath));
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
            if (existAssembly != null)
            {
                return existAssembly;
            }

            // The same applies to identical assemblies the host ships but has not loaded yet (e.g. MemoryPack.Core:
            // a plugin-local copy would keep its own formatter registry, so the host's BinaryStorage could not
            // serialize plugin types). Returning null defers to the default context, which loads the host's copy.
            if (HostAssemblyFullNames.Value.Contains(assemblyName.FullName))
            {
                return null;
            }

            return assemblyPath == null ? null : LoadFromAssemblyPath(assemblyPath);
        }

        // Full names of the managed assemblies on the host's trusted platform assembly list (the app's own deps).
        private static readonly Lazy<HashSet<string>> HostAssemblyFullNames = new(() =>
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;
            foreach (var path in tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    names.Add(AssemblyName.GetAssemblyName(path).FullName);
                }
                catch (Exception e) when (e is BadImageFormatException or FileLoadException or FileNotFoundException)
                {
                    // Not a managed assembly or no longer present; it cannot be shared anyway.
                }
            }

            return names;
        });
        
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
