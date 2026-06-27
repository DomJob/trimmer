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
        var handler = new ServeRequestHandler(root, resolver, liveReload);

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(o => o.SingleLine = true);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        var app = builder.Build();
        app.Urls.Add($"http://localhost:{port}");

        using var watcher = new ProjectWatcher(root, () =>
        {
            handler.Compiler.Invalidate();
            liveReload.NotifyChange();
        });
        watcher.Start();

        app.Run(context => handler.HandleAsync(context));

        Console.WriteLine($"trimmer serving {root}");
        Console.WriteLine($"  http://localhost:{port}  (hot reload enabled)");
        Console.WriteLine("Press Ctrl+C to stop.");

        await app.StartAsync(cancellationToken);
        await app.WaitForShutdownAsync(cancellationToken);
    }
}
