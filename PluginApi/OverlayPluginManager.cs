using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace PerformanceOverlay.PluginApi;

public sealed class LoadedOverlayPlugin : IDisposable
{
    private readonly Action _unload;
    private bool _disposed;

    internal LoadedOverlayPlugin(
        IOverlayPlugin instance,
        System.Windows.FrameworkElement view,
        string assemblyPath,
        Action unload)
    {
        Instance = instance;
        View = view;
        AssemblyPath = assemblyPath;
        _unload = unload;
    }

    public IOverlayPlugin Instance { get; }
    public System.Windows.FrameworkElement View { get; }
    public string AssemblyPath { get; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { Instance.Dispose(); }
        finally { _unload(); }
    }
}

public sealed class OverlayPluginManager
{
    public LoadedOverlayPlugin Load(string assemblyPath)
    {
        var fullPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The selected plugin assembly was not found.", fullPath);

        var loadContext = new PluginAssemblyLoadContext(fullPath);
        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(fullPath);
            var pluginTypes = assembly.GetTypes()
                .Where(type => type.IsPublic && !type.IsAbstract && !type.IsInterface &&
                    typeof(IOverlayPlugin).IsAssignableFrom(type) &&
                    type.GetConstructor(Type.EmptyTypes) is not null)
                .ToArray();

            if (pluginTypes.Length != 1)
                throw new InvalidOperationException(
                    "A plugin assembly must contain exactly one public, parameterless IOverlayPlugin implementation.");

            var instance = (IOverlayPlugin?)Activator.CreateInstance(pluginTypes[0])
                ?? throw new InvalidOperationException("The plugin could not be created.");
            System.Windows.FrameworkElement view;
            try
            {
                view = instance.CreateView()
                    ?? throw new InvalidOperationException("The plugin did not provide an overlay view.");
            }
            catch
            {
                try { instance.Dispose(); }
                catch { }
                throw;
            }

            return new LoadedOverlayPlugin(instance, view, fullPath, loadContext.Unload);
        }
        catch
        {
            loadContext.Unload();
            throw;
        }
    }

    private sealed class PluginAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        private readonly string _pluginApiName = typeof(IOverlayPlugin).Assembly.GetName().Name!;

        public PluginAssemblyLoadContext(string pluginAssemblyPath)
            : base(Path.GetFileNameWithoutExtension(pluginAssemblyPath), isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name == _pluginApiName)
                return null;

            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            return assemblyPath is null ? null : LoadFromAssemblyPath(assemblyPath);
        }

        protected override nint LoadUnmanagedDll(string unmanagedDllName)
        {
            var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return libraryPath is null ? nint.Zero : LoadUnmanagedDllFromPath(libraryPath);
        }
    }
}
