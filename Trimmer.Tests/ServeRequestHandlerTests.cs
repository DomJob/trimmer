using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Trimmer.Hosting;
using Trimmer.Packages;

namespace Trimmer.Tests;

public class ServeRequestHandlerTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Directory.CreateTempSubdirectory("trimmer_serve_").FullName;
        File.WriteAllText(Path.Combine(_root, "shared.cs"),
            "namespace App; public static class M { public static int Double(int n) => n * 2; }");
        File.WriteAllText(Path.Combine(_root, "index.cshtml"),
            "@using App\n<html><body><p>@M.Double(21)</p></body></html>");
        File.WriteAllText(Path.Combine(_root, "secret.cs"), "class Secret {}");
        Directory.CreateDirectory(Path.Combine(_root, "assets"));
        File.WriteAllText(Path.Combine(_root, "assets", "x.txt"), "asset");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    private static async Task<(int Status, string Body, string? ContentType)> RequestAsync(
        ServeRequestHandler handler, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var body = new MemoryStream();
        context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(body));

        await handler.HandleAsync(context);

        body.Position = 0;
        using var reader = new StreamReader(body);
        return (context.Response.StatusCode, await reader.ReadToEndAsync(), context.Response.ContentType);
    }

    private ServeRequestHandler CreateHandler() =>
        new(_root, new FakePackageResolver(), new LiveReloadService());

    [Test]
    public async Task Renders_page_and_injects_live_reload()
    {
        var (status, body, contentType) = await RequestAsync(CreateHandler(), "/");
        Assert.That(status, Is.EqualTo(200));
        Assert.That(body, Does.Contain("<p>42</p>"));
        Assert.That(body, Does.Contain("EventSource"));
        Assert.That(contentType, Does.Contain("text/html"));
    }

    [Test]
    public async Task Cs_file_is_forbidden()
    {
        var (status, _, _) = await RequestAsync(CreateHandler(), "/secret.cs");
        Assert.That(status, Is.EqualTo(403));
    }

    [Test]
    public async Task Static_asset_is_served()
    {
        var (status, body, _) = await RequestAsync(CreateHandler(), "/assets/x.txt");
        Assert.That(status, Is.EqualTo(200));
        Assert.That(body, Is.EqualTo("asset"));
    }

    [Test]
    public async Task Missing_route_returns_404()
    {
        var (status, _, _) = await RequestAsync(CreateHandler(), "/nope");
        Assert.That(status, Is.EqualTo(404));
    }

    [Test]
    public async Task Compilation_error_returns_500()
    {
        File.WriteAllText(Path.Combine(_root, "broken.cshtml"), "@(this is not valid c#)");
        var (status, _, _) = await RequestAsync(CreateHandler(), "/broken");
        Assert.That(status, Is.EqualTo(500));
    }
}

/// <summary>
/// Exercises real NuGet package resolution via <c>#:package</c>. Marked Explicit because it
/// shells out to the .NET SDK and downloads from nuget.org.
/// </summary>
[Explicit("Requires network access and the .NET SDK to restore packages.")]
[Category("Integration")]
public class PackageResolutionIntegrationTests
{
    [Test]
    public async Task Page_can_use_a_nuget_package()
    {
        var cache = Directory.CreateTempSubdirectory("trimmer_pkg_cache_").FullName;
        var project = Directory.CreateTempSubdirectory("trimmer_pkg_proj_").FullName;
        try
        {
            var indexPath = Path.Combine(project, "index.cshtml");
            File.WriteAllText(indexPath,
                """
                #:package Humanizer@2.14.1
                @using Humanizer
                <p>@(3.ToWords())</p>
                """);

            var resolver = new NuGetPackageResolver(cacheRoot: cache);
            var compiler = new ProjectCompiler(project, resolver);

            var page = await compiler.GetPageAsync(indexPath);
            var html = await page.CreateInstance().RenderAsync();

            Assert.That(html, Does.Contain("three"));
        }
        finally
        {
            Directory.Delete(project, recursive: true);
            Directory.Delete(cache, recursive: true);
        }
    }
}
