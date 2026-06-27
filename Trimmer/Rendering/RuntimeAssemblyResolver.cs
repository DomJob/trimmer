using System.Collections.Concurrent;
using System.Runtime.Loader;

namespace Trimmer.Rendering;

/// <summary>
/// Makes resolved NuGet assemblies loadable at runtime. Compiled pages reference these
/// assemblies, so when a page actually invokes a package type the CLR must be able to
/// locate it. A single resolver is installed on the default load context.
/// </summary>
public static class RuntimeAssemblyResolver
{
    private static readonly ConcurrentDictionary<string, string> AssembliesByName =
        new(StringComparer.OrdinalIgnoreCase);

    private static int _installed;

    /// <summary>Registers assembly paths so they can be resolved on demand at runtime.</summary>
    public static void Register(IEnumerable<string> assemblyPaths)
    {
        foreach (var path in assemblyPaths)
        {
            if (File.Exists(path) && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                AssembliesByName[Path.GetFileNameWithoutExtension(path)] = path;
            }
        }

        Install();
    }

    private static void Install()
    {
        if (Interlocked.Exchange(ref _installed, 1) == 1)
        {
            return;
        }

        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            if (name.Name is not null
                && AssembliesByName.TryGetValue(name.Name, out var path)
                && File.Exists(path))
            {
                return context.LoadFromAssemblyPath(path);
            }

            return null;
        };
    }
}
