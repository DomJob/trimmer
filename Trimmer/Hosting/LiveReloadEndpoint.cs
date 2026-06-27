using Microsoft.AspNetCore.Http;

namespace Trimmer.Hosting;

/// <summary>Streams Server-Sent Events that tell the browser to reload after a change.</summary>
public static class LiveReloadEndpoint
{
    public static async Task StreamAsync(HttpContext context, LiveReloadService liveReload)
    {
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";
        context.Response.ContentType = "text/event-stream";

        await context.Response.WriteAsync(": connected\n\n");
        await context.Response.Body.FlushAsync();

        while (!context.RequestAborted.IsCancellationRequested)
        {
            try
            {
                await liveReload.WaitForChangeAsync(context.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await context.Response.WriteAsync("data: reload\n\n");
            await context.Response.Body.FlushAsync();
        }
    }
}
