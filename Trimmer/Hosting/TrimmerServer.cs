using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Trimmer.Packages;

namespace Trimmer.Hosting;

/// <summary>Builds and runs the Kestrel host for <c>trimmer serve</c>.</summary>
public static class TrimmerServer
{
    public static async Task ServeAsync(string root, int port, IPackageResolver resolver, CancellationToken cancellationToken = default)
    {
        root = Path.GetFullPath(root);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Directory '{root}' does not exist.");
        }

        var liveReload = new LiveReloadService();

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(o => o.SingleLine = true);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        var app = builder.Build();
        app.Urls.Add($"http://localhost:{port}");

        var handler = new ServeRequestHandler(root, resolver, liveReload, app.Lifetime.ApplicationStopping);

        using var watcher = new ProjectWatcher(root, changed =>
        {
            handler.Compiler.Invalidate();

            // A page only needs to reload when the change is relevant to it: the page's own
            // .cshtml, or any .cs file (shared C# is compiled into every page). Changes to
            // unrelated pages or static assets leave the current page untouched.
            var sharedCodeChanged = changed.Any(IsCSharpSource);
            var changedPages = changed
                .Where(IsRazorPage)
                .Select(p => PageKey(root, p))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            liveReload.NotifyChange(pageKey =>
                sharedCodeChanged || changedPages.Contains(pageKey));
        });
        watcher.Start();

        app.Run(context => handler.HandleAsync(context));

        Console.WriteLine($"trimmer serving {root}");
        Console.WriteLine($"  http://localhost:{port}  (hot reload enabled)");
        Console.WriteLine("Press Ctrl+C to stop.");

        await app.StartAsync(cancellationToken);
        await app.WaitForShutdownAsync(cancellationToken);
    }

    private static bool IsRazorPage(string path) =>
        path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase);

    private static bool IsCSharpSource(string path) =>
        path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

    /// <summary>The page key shared between the browser and server: the page's path relative to the root.</summary>
    internal static string PageKey(string root, string cshtmlPath) =>
        Path.GetRelativePath(root, cshtmlPath).Replace('\\', '/');
}


