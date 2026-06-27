using System.Collections.Concurrent;
using Trimmer.Build;
using Trimmer.Packages;
using Trimmer.Rendering;

namespace Trimmer.Hosting;

/// <summary>
/// Compiles pages on demand while serving, combining a page with the project's shared
/// <c>.cs</c> files and resolved packages. Compiled pages are cached until the project
/// changes (see <see cref="Invalidate"/>), which is what makes hot reload cheap.
/// </summary>
public sealed class ProjectCompiler(string root, IPackageResolver packageResolver)
{
    private readonly ConcurrentDictionary<string, CompiledPage> _cache = new(StringComparer.OrdinalIgnoreCase);

    public string Root { get; } = Path.GetFullPath(root);

    /// <summary>Drops all cached pages so the next request recompiles from disk.</summary>
    public void Invalidate() => _cache.Clear();

    public async Task<CompiledPage> GetPageAsync(string cshtmlPath, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(cshtmlPath, out var cached))
        {
            return cached;
        }

        var compiled = await CompileAsync(cshtmlPath, cancellationToken);
        _cache[cshtmlPath] = compiled;
        return compiled;
    }

    private async Task<CompiledPage> CompileAsync(string cshtmlPath, CancellationToken cancellationToken)
    {
        var pageScan = PackageDirectiveParser.Scan(await File.ReadAllTextAsync(cshtmlPath, cancellationToken));
        var project = ProjectScanner.LoadCSharp(Root);

        var packages = PackageDirectiveParser.Collect([])
            .Concat(project.Packages)
            .Concat(pageScan.Packages)
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var assemblies = await packageResolver.ResolveAsync(packages, cancellationToken);
        return RazorPageCompiler.Compile(pageScan.Content, project.CSharpSources, assemblies);
    }
}
