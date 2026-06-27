using Microsoft.CodeAnalysis;

namespace Trimmer.Rendering;

/// <summary>
/// Builds the set of <see cref="MetadataReference"/>s used to compile generated pages.
/// Starts from the framework assemblies available to the running tool (which already
/// include ASP.NET Core) and layers any resolved NuGet assemblies on top.
/// </summary>
public static class ReferenceResolver
{
    /// <summary>
    /// Combines the trusted platform assemblies with the supplied extra assembly paths,
    /// de-duplicating by simple file name (NuGet assemblies win over framework ones).
    /// </summary>
    public static IReadOnlyList<MetadataReference> Resolve(IEnumerable<string>? extraAssemblyPaths = null)
    {
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var trusted = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string) ?? string.Empty;
        foreach (var path in trusted.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                byName[Path.GetFileNameWithoutExtension(path)] = path;
            }
        }

        if (extraAssemblyPaths is not null)
        {
            foreach (var path in extraAssemblyPaths)
            {
                if (File.Exists(path) && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    byName[Path.GetFileNameWithoutExtension(path)] = path;
                }
            }
        }

        var references = new List<MetadataReference>(byName.Count);
        foreach (var path in byName.Values)
        {
            try
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
            catch (IOException)
            {
                // Skip assemblies that cannot be read as metadata.
            }
        }

        return references;
    }
}
