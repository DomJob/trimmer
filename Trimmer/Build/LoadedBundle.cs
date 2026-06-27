using System.IO.Compression;
using System.Runtime.Loader;
using Trimmer.Rendering;

namespace Trimmer.Build;

/// <summary>
/// A compiled <c>.trm</c> bundle extracted and loaded into memory, ready to serve.
/// Routes map to compiled page types and static assets live in <see cref="AssetsDirectory"/>.
/// </summary>
public sealed class LoadedBundle : IDisposable
{
    private readonly string _workingDirectory;

    private LoadedBundle(string workingDirectory, string assetsDirectory, IReadOnlyDictionary<string, CompiledPage> pages)
    {
        _workingDirectory = workingDirectory;
        AssetsDirectory = assetsDirectory;
        Pages = pages;
    }

    /// <summary>Directory containing the extracted static assets.</summary>
    public string AssetsDirectory { get; }

    /// <summary>Normalized route -> compiled page.</summary>
    public IReadOnlyDictionary<string, CompiledPage> Pages { get; }

    public static LoadedBundle Load(string bundlePath)
    {
        bundlePath = Path.GetFullPath(bundlePath);
        if (!File.Exists(bundlePath))
        {
            throw new FileNotFoundException($"Bundle '{bundlePath}' not found.", bundlePath);
        }

        var workingDirectory = Directory.CreateTempSubdirectory("trimmer_run_").FullName;
        ZipFile.ExtractToDirectory(bundlePath, workingDirectory);

        var manifestPath = Path.Combine(workingDirectory, BundleManifest.FileName);
        if (!File.Exists(manifestPath))
        {
            Directory.Delete(workingDirectory, recursive: true);
            throw new InvalidOperationException("Bundle is missing its manifest; was it produced by 'trimmer build'?");
        }

        var manifest = BundleManifest.FromJson(File.ReadAllText(manifestPath));
        var libDirectory = Path.Combine(workingDirectory, "lib");
        if (Directory.Exists(libDirectory))
        {
            RuntimeAssemblyResolver.Register(Directory.EnumerateFiles(libDirectory, "*.dll"));
        }

        var assemblyPath = Path.Combine(workingDirectory, manifest.Assembly);
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);

        var pages = new Dictionary<string, CompiledPage>(StringComparer.Ordinal);
        foreach (var (route, typeName) in manifest.Routes)
        {
            var type = assembly.GetType(typeName, throwOnError: true)!;
            pages[RouteNaming.NormalizeRequest(route)] = new CompiledPage(type);
        }

        var assetsDirectory = Path.Combine(workingDirectory, "wwwroot");
        Directory.CreateDirectory(assetsDirectory);

        return new LoadedBundle(workingDirectory, assetsDirectory, pages);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Assemblies stay loaded for the process lifetime; ignore cleanup failures.
        }
    }
}
