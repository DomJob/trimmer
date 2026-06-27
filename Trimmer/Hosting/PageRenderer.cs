using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Trimmer.Rendering;

namespace Trimmer.Hosting;

/// <summary>Renders a compiled page using the current request context.</summary>
public static class PageRenderer
{
    public static async Task<string> RenderAsync(CompiledPage page, HttpContext context)
    {
        var instance = page.CreateInstance();
        instance.Context = context;
        return await instance.RenderAsync();
    }
}

/// <summary>Low-level helpers for writing HTTP responses.</summary>
public static class ResponseWriter
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public static async Task WriteHtmlAsync(HttpContext context, string html)
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(html);
    }

    public static async Task WriteStaticAsync(HttpContext context, string path)
    {
        context.Response.ContentType = ContentTypes.TryGetContentType(path, out var contentType)
            ? contentType
            : "application/octet-stream";

        await context.Response.SendFileAsync(path);
    }

    public static Task WriteNotFoundAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "text/html; charset=utf-8";
        return context.Response.WriteAsync("<h1>404 - Not Found</h1>");
    }

    public static Task WriteForbiddenAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "text/html; charset=utf-8";
        return context.Response.WriteAsync("<h1>403 - Forbidden</h1>");
    }

    public static Task WriteErrorAsync(HttpContext context, Exception exception)
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "text/html; charset=utf-8";
        var safe = System.Net.WebUtility.HtmlEncode(exception.Message);
        return context.Response.WriteAsync($"<h1>500 - Server Error</h1><pre>{safe}</pre>");
    }
}
