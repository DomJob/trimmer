namespace Trimmer.Rendering;

/// <summary>A compiled page ready to be instantiated and rendered.</summary>
public sealed class CompiledPage(Type pageType)
{
    public Type PageType { get; } = pageType;

    public TrimmerPage CreateInstance()
    {
        if (Activator.CreateInstance(PageType) is not TrimmerPage page)
        {
            throw new InvalidOperationException($"Type '{PageType.FullName}' is not a TrimmerPage.");
        }

        return page;
    }
}

/// <summary>
/// Orchestrates the full pipeline: Razor markup + companion <c>.cs</c> files become a
/// compiled, loadable page type.
/// </summary>
public static class RazorPageCompiler
{
    private static int counter;

    /// <summary>Compiles a single page together with the project's shared <c>.cs</c> sources.</summary>
    /// <param name="cshtml">Razor markup with <c>#:package</c> directives already removed.</param>
    /// <param name="csharpSources">Companion C# sources (already package-stripped).</param>
    /// <param name="extraAssemblies">Resolved NuGet assembly paths.</param>
    public static CompiledPage Compile(
        string cshtml,
        IEnumerable<string>? csharpSources = null,
        IEnumerable<string>? extraAssemblies = null)
    {
        var className = "Page_" + Interlocked.Increment(ref counter).ToString("D");
        var generated = RazorSourceGenerator.Generate(cshtml, className);

        var sources = new List<string> { generated };
        if (csharpSources is not null)
        {
            sources.AddRange(csharpSources);
        }

        var resolved = extraAssemblies?.ToList() ?? [];

        // Ensure the referenced package assemblies can also be loaded at runtime, not just
        // referenced at compile time, since the page invokes their types when it executes.
        RuntimeAssemblyResolver.Register(resolved);

        var references = ReferenceResolver.Resolve(resolved);
        var assemblyName = "TrimmerPages_" + Guid.NewGuid().ToString("N");
        var assembly = RoslynCompiler.CompileToAssembly(assemblyName, sources, references);

        var pageType = assembly.GetType($"{RazorSourceGenerator.GeneratedNamespace}.{className}", throwOnError: true)!;
        return new CompiledPage(pageType);
    }
}
