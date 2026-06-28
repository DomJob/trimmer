using Microsoft.AspNetCore.Http;
using Trimmer.Packages;

namespace Trimmer.Hosting;

/// <summary>Handles requests in <c>serve</c> mode: compile-on-demand with hot reload.</summary>
public sealed class ServeRequestHandler
{
    private readonly FileRouter _router;
    private readonly LiveReloadService _liveReload;
    private readonly CancellationToken _shutdownToken;

    public ServeRequestHandler(string root, IPackageResolver resolver, LiveReloadService liveReload, CancellationToken shutdownToken = default)
    {
        _router = new FileRouter(root);
        _liveReload = liveReload;
        _shutdownToken = shutdownToken;
        Compiler = new ProjectCompiler(root, resolver);
    }

    public ProjectCompiler Compiler { get; }

    public async Task HandleAsync(HttpContext context)
    {
        var path = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";

        if (path == LiveReloadService.Endpoint)
        {
            await LiveReloadEndpoint.StreamAsync(context, _liveReload, _shutdownToken);
            return;
        }

        try
        {
            var route = _router.Resolve(path);
            switch (route.Kind)
            {
                case RouteKind.Razor:
                    var page = await Compiler.GetPageAsync(route.PhysicalPath!, context.RequestAborted);
                    var html = await PageRenderer.RenderAsync(page, context);
                    var pageKey = TrimmerServer.PageKey(Compiler.Root, route.PhysicalPath!);
                    await ResponseWriter.WriteHtmlAsync(context, _liveReload.InjectInto(html, pageKey));
                    break;

                case RouteKind.Static:
                    await ResponseWriter.WriteStaticAsync(context, route.PhysicalPath!);
                    break;

                case RouteKind.Forbidden:
                    await ResponseWriter.WriteForbiddenAsync(context);
                    break;

                case RouteKind.NotFound:
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
