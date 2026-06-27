namespace Trimmer.Hosting;

public enum RouteKind
{
    NotFound,
    Forbidden,
    Razor,
    Static,
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
    private readonly string _root;

    public FileRouter(string rootDirectory)
    {
        _root = Path.GetFullPath(rootDirectory);
    }

    public RouteResult Resolve(string requestPath)
    {
        var relative = (requestPath ?? string.Empty).Replace('\\', '/').TrimStart('/');

        if (relative.Length == 0)
        {
            relative = DefaultDocument;
        }
        else if (relative.EndsWith('/'))
        {
            relative += DefaultDocument;
        }

        // Resolve to an absolute path and guard against directory traversal.
        var candidate = Path.GetFullPath(Path.Combine(_root, relative));
        if (!IsUnderRoot(candidate))
        {
            return RouteResult.Forbidden;
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

        // Extensionless route -> <name>.cshtml (PHP-style clean URLs).
        if (!Path.HasExtension(candidate))
        {
            var asRazor = candidate + ".cshtml";
            if (File.Exists(asRazor))
            {
                return RouteResult.Razor(asRazor);
            }

            var dirDefault = Path.Combine(candidate, DefaultDocument);
            if (File.Exists(dirDefault))
            {
                return RouteResult.Razor(dirDefault);
            }
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
