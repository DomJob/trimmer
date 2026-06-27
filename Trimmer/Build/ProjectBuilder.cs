using System.IO.Compression;
using Trimmer.Packages;
using Trimmer.Rendering;

namespace Trimmer.Build;

/// <summary>
/// Compiles an entire project into a single, compact <c>.trmr</c> bundle: one assembly
/// containing every page, a route manifest, the resolved NuGet assemblies and static assets.
/// </summary>
public sealed class ProjectBuilder(IPackageResolver packageResolver)
{
    public async Task<string> BuildAsync(string root, string? outputPath = null, CancellationToken cancellationToken = default)
    {
        root = Path.GetFullPath(root);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Directory '{root}' does not exist.");
        }

        outputPath = Path.GetFullPath(outputPath ?? new DirectoryInfo(root).Name + ".trmr");

        var project = ProjectScanner.LoadCSharp(root);
        var sources = new List<string>(project.CSharpSources);
        var packages = project.Packages.ToList();
        var seenPackages = new HashSet<string>(packages.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);

        var manifest = new BundleManifest();
        var usedClassNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in ProjectScanner.EnumerateRazorFiles(root).OrderBy(p => p, StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(root, file);
            var route = RouteNaming.RouteForFile(relative);
            var className = MakeUnique(RouteNaming.ClassNameForRoute(route), usedClassNames);

            var scan = PackageDirectiveParser.Scan(await File.ReadAllTextAsync(file, cancellationToken));
            foreach (var package in scan.Packages)
            {
                if (seenPackages.Add(package.Name))
                {
                    packages.Add(package);
                }
            }

            sources.Add(RazorSourceGenerator.Generate(scan.Content, className));
            manifest.Routes[route] = $"{RazorSourceGenerator.GeneratedNamespace}.{className}";
        }

        if (manifest.Routes.Count == 0)
        {
            throw new InvalidOperationException($"No .cshtml pages found in '{root}'.");
        }

        var libraries = await packageResolver.ResolveAsync(packages, cancellationToken);
        var references = ReferenceResolver.Resolve(libraries);

        var staging = Directory.CreateTempSubdirectory("trimmer_build_");
        try
        {
            RoslynCompiler.CompileToFile(
                "TrimmerPages",
                sources,
                references,
                Path.Combine(staging.FullName, manifest.Assembly));

            CopyLibraries(libraries, staging.FullName, manifest);
            CopyStaticAssets(root, staging.FullName);

            await File.WriteAllTextAsync(
                Path.Combine(staging.FullName, BundleManifest.FileName),
                manifest.ToJson(),
                cancellationToken);

            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            ZipFile.CreateFromDirectory(staging.FullName, outputPath, CompressionLevel.SmallestSize, includeBaseDirectory: false);
        }
        finally
        {
            staging.Delete(recursive: true);
        }

        return outputPath;
    }

    private static void CopyLibraries(IReadOnlyList<string> libraries, string stagingDir, BundleManifest manifest)
    {
        if (libraries.Count == 0)
        {
            return;
        }

        var libDir = Path.Combine(stagingDir, "lib");
        Directory.CreateDirectory(libDir);
        foreach (var library in libraries)
        {
            var name = Path.GetFileName(library);
            File.Copy(library, Path.Combine(libDir, name), overwrite: true);
            manifest.Libraries.Add(name);
        }
    }

    private static void CopyStaticAssets(string root, string stagingDir)
    {
        var wwwroot = Path.Combine(stagingDir, "wwwroot");
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(file);
            if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".cshtml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relative = Path.GetRelativePath(root, file);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Any(s => s is "bin" or "obj" or ".git" or ".trimmer"))
            {
                continue;
            }

            var target = Path.Combine(wwwroot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static string MakeUnique(string name, HashSet<string> used)
    {
        if (string.IsNullOrEmpty(name))
        {
            name = "Page";
        }

        var candidate = name;
        var counter = 1;
        while (!used.Add(candidate))
        {
            candidate = $"{name}_{counter++}";
        }

        return candidate;
    }
}
