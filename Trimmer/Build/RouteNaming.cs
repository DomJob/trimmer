using System.Text;

namespace Trimmer.Build;

/// <summary>Converts file paths to request routes and generated class names.</summary>
public static class RouteNaming
{
    /// <summary>Computes the public request route for a <c>.cshtml</c> file relative to the project root.</summary>
    /// <example><c>blog/index.cshtml</c> -> <c>/blog</c>, <c>about.cshtml</c> -> <c>/about</c>, <c>index.cshtml</c> -> <c>/</c>.</example>
    public static string RouteForFile(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        if (normalized.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^".cshtml".Length];
        }

        if (normalized.Equals("index", StringComparison.OrdinalIgnoreCase))
        {
            return "/";
        }

        if (normalized.EndsWith("/index", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^"/index".Length];
        }

        return "/" + normalized.TrimStart('/');
    }

    /// <summary>Normalizes an incoming request path so it can be matched against routes.</summary>
    public static string NormalizeRequest(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
        {
            return "/";
        }

        var normalized = path.Replace('\\', '/');
        if (normalized.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^".cshtml".Length];
        }

        normalized = normalized.TrimEnd('/');
        return normalized.Length == 0 ? "/" : normalized;
    }

    /// <summary>Builds a unique, valid C# class name for a route.</summary>
    public static string ClassNameForRoute(string route)
    {
        var builder = new StringBuilder("Page");
        foreach (var c in route)
        {
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        // Collapse the leading separators produced by "/".
        return builder.ToString().TrimEnd('_');
    }
}
