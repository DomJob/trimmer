using Microsoft.AspNetCore.Http;

namespace Trimmer.Hosting;

/// <summary>Streams Server-Sent Events that tell the browser to reload after a change.</summary>
public static class LiveReloadEndpoint
{
    public static async Task StreamAsync(HttpContext context, LiveReloadService liveReload, CancellationToken shutdownToken)
    {
        // Exit the loop both when the client disconnects and when the host begins shutting
        // down, so a held-open SSE connection never blocks a graceful Ctrl+C shutdown.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, shutdownToken);
        var token = linked.Token;

        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";
        context.Response.ContentType = "text/event-stream";

        var pageKey = context.Request.Query[LiveReloadService.PageQueryKey].ToString();
        using var connection = liveReload.Connect(pageKey);

        await context.Response.WriteAsync(": connected\n\n", token);
        await context.Response.Body.FlushAsync(token);

        while (!token.IsCancellationRequested)
        {
            try
            {
                await connection.WaitForChangeAsync(token);
                await context.Response.WriteAsync("data: reload\n\n", token);
                await context.Response.Body.FlushAsync(token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
