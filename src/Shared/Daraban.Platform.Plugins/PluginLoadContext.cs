using System.Reflection;
using System.Runtime.Loader;

namespace Daraban.Platform.Plugins;

/// <summary>
/// Isolated, collectible <see cref="AssemblyLoadContext"/> that loads a plugin's assembly
/// and its dependencies from the plugin's own directory. The host's assemblies are
/// deliberately <em>not</em> resolvable here (beyond the shared contract assembly), so a
/// plugin cannot bind against or accidentally load host internals.
/// </summary>
/// <remarks>
/// IsCollectible = true allows unloading: once the context (and every type it created)
/// is unreferenced, <see cref="AssemblyLoadContext.Unload"/> returns the memory and the
/// plugin's code disappears from the process. Unloading only works if no host code holds
/// references to plugin types -- the manager therefore only ever touches plugins through
/// the shared <c>Daraban.Platform.Plugins</c> abstractions.
/// </remarks>
public sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginId;

    /// <summary>Creates a context bound to the plugin directory resolved from <paramref name="pluginAssemblyPath"/>.</summary>
    public PluginLoadContext(string pluginId, string pluginAssemblyPath)
        : base(name: $"Plugin:{pluginId}", isCollectible: true)
    {
        _pluginId = pluginId;
        _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
    }

    /// <summary>The plugin this context was created for.</summary>
    public string PluginId => _pluginId;

    /// <summary>
    /// Loads the plugin's own assembly and resolves plugin-local dependencies (from the
    /// plugin's <c>.deps.json</c> if present). Framework assemblies fall through to the
    /// default ALC so the plugin shares the host's runtime; everything else is refused.
    /// </summary>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Never let a plugin provide its own copy of the plugin contract assembly:
        // identity must stay with the host or casts to IPlugin would fail.
        if (assemblyName.Name == typeof(IPlugin).Assembly.GetName().Name)
            return null;

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path is not null)
        {
            return LoadFromAssemblyPath(path);
        }

        // Framework + platform assemblies resolve through the default context.
        return null;
    }

    /// <summary>Unmanaged libraries are resolved from the plugin directory only.</summary>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is not null ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
    }
}
