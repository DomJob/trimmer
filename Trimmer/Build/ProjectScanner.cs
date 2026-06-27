using Trimmer.Packages;

namespace Trimmer.Build;

/// <summary>The companion C# sources and package references discovered in a project.</summary>
public sealed record ProjectSources(
    IReadOnlyList<string> CSharpSources,
    IReadOnlyList<PackageReference> Packages);

/// <summary>Discovers the files that make up a Trimmer project on disk.</summary>
public static class ProjectScanner
{
    private static readonly string[] IgnoredDirectories = ["bin", "obj", ".git", ".trimmer"];

    public static IEnumerable<string> EnumerateCSharpFiles(string root) =>
        EnumerateFiles(root, ".cs");

    public static IEnumerable<string> EnumerateRazorFiles(string root) =>
        EnumerateFiles(root, ".cshtml");

    /// <summary>Reads every companion <c>.cs</c> file, strips package directives and collects them.</summary>
    public static ProjectSources LoadCSharp(string root)
    {
        var sources = new List<string>();
        var packages = new List<PackageReference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in EnumerateCSharpFiles(root).OrderBy(p => p, StringComparer.Ordinal))
        {
            var scan = PackageDirectiveParser.Scan(File.ReadAllText(file));
            sources.Add(scan.Content);
            foreach (var package in scan.Packages)
            {
                if (seen.Add(package.Name))
                {
                    packages.Add(package);
                }
            }
        }

        return new ProjectSources(sources, packages);
    }

    private static IEnumerable<string> EnumerateFiles(string root, string extension)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(root, "*" + extension, SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Any(s => IgnoredDirectories.Contains(s, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            yield return file;
        }
    }
}
