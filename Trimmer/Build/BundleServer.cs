using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Trimmer.Hosting;

namespace Trimmer.Build;

/// <summary>Handles requests in <c>run</c> mode using a pre-compiled bundle.</summary>
public sealed class BundleRequestHandler(LoadedBundle bundle)
{
    private readonly FileRouter _staticRouter = new(bundle.AssetsDirectory);

    public async Task HandleAsync(HttpContext context)
    {
        var path = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";

        try
        {
            var route = RouteNaming.NormalizeRequest(path);
            if (bundle.Pages.TryGetValue(route, out var page))
            {
                var html = await PageRenderer.RenderAsync(page, context);
                await ResponseWriter.WriteHtmlAsync(context, html);
                return;
            }

            var staticRoute = _staticRouter.Resolve(path);
            switch (staticRoute.Kind)
            {
                case RouteKind.Static:
                    await ResponseWriter.WriteStaticAsync(context, staticRoute.PhysicalPath!);
                    break;
                case RouteKind.Forbidden:
                    await ResponseWriter.WriteForbiddenAsync(context);
                    break;
                default:
                    await ResponseWriter.WriteNotFoundAsync(context);
                    break;
            }
        }
        catch (Exception ex)
        {
            await ResponseWriter.WriteErrorAsync(context, ex);
        }
    }
}

/// <summary>Builds and runs the Kestrel host for <c>trimmer run</c>.</summary>
public static class BundleServer
{
    public static async Task RunAsync(string bundlePath, int port, CancellationToken cancellationToken = default)
    {
        using var bundle = LoadedBundle.Load(bundlePath);
        var handler = new BundleRequestHandler(bundle);

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(o => o.SingleLine = true);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        var app = builder.Build();
        app.Urls.Add($"http://localhost:{port}");
        app.Run(context => handler.HandleAsync(context));

        Console.WriteLine($"trimmer running bundle {Path.GetFullPath(bundlePath)}");
        Console.WriteLine($"  http://localhost:{port}");
        Console.WriteLine("Press Ctrl+C to stop.");

        await app.StartAsync(cancellationToken);
        await app.WaitForShutdownAsync(cancellationToken);
    }
}
