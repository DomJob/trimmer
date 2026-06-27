namespace Trimmer.Hosting;

public enum RouteKind
{
    NotFound,
    Forbidden,
    Razor,
    Static
}

public readonly record struct RouteResult(RouteKind Kind, string? PhysicalPath)
{
    public static readonly RouteResult NotFound = new(RouteKind.NotFound, null);
    public static readonly RouteResult Forbidden = new(RouteKind.Forbidden, null);

    public static RouteResult Razor(string path) => new(RouteKind.Razor, path);

    public static RouteResult Static(string path) => new(RouteKind.Static, path);
}

/// <summary>
/// Maps an incoming request path to a file inside the served root directory.
/// <c>.cshtml</c> files are rendered, <c>.cs</c> files are never exposed, and
/// everything else is served as a static asset.
/// </summary>
public sealed class FileRouter
{
    private const string DefaultDocument = "index.cshtml";
    private const string DefaultStaticDocument = "index.html";
    private readonly string _root;

    public FileRouter(string rootDirectory)
    {
        _root = Path.GetFullPath(rootDirectory);
    }

    public RouteResult Resolve(string requestPath)
    {
        var relative = requestPath.Replace('\\', '/').TrimStart('/');

        var wantsDirectoryDefault = relative.Length == 0 || relative.EndsWith('/');

        // Resolve to an absolute path and guard against directory traversal.
        var candidate = Path.GetFullPath(Path.Combine(_root, relative));
        if (!IsUnderRoot(candidate))
        {
            return RouteResult.Forbidden;
        }

        if (wantsDirectoryDefault)
        {
            return ResolveDirectoryDefault(candidate);
        }

        if (HasExtension(candidate, ".cs"))
        {
            return RouteResult.Forbidden;
        }

        if (File.Exists(candidate))
        {
            return HasExtension(candidate, ".cshtml")
                ? RouteResult.Razor(candidate)
                : RouteResult.Static(candidate);
        }

        // Extensionless route -> <name>.cshtml
        if (Path.HasExtension(candidate)) return RouteResult.NotFound;
        var asRazor = candidate + ".cshtml";
        if (File.Exists(asRazor))
        {
            return RouteResult.Razor(asRazor);
        }

        return ResolveDirectoryDefault(candidate);
    }

    /// <summary>
    /// Resolves the default document for a directory, preferring
    /// <c>index.cshtml</c> and falling back to <c>index.html</c>.
    /// </summary>
    private RouteResult ResolveDirectoryDefault(string directory)
    {
        var razor = Path.Combine(directory, DefaultDocument);
        if (File.Exists(razor))
        {
            return RouteResult.Razor(razor);
        }

        var html = Path.Combine(directory, DefaultStaticDocument);
        if (File.Exists(html))
        {
            return RouteResult.Static(html);
        }

        return RouteResult.NotFound;
    }

    private bool IsUnderRoot(string fullPath)
    {
        var normalizedRoot = _root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.Equals(_root, StringComparison.Ordinal)
            || fullPath.StartsWith(normalizedRoot, StringComparison.Ordinal);
    }

    private static bool HasExtension(string path, string extension) =>
        Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase);
}
