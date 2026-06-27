using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Trimmer.Build;

namespace Trimmer.Tests;

/// <summary>
/// End-to-end coverage of the build -> run pipeline: a project on disk is compiled into a
/// bundle, loaded back and served through the request handler (without a live socket).
/// </summary>
public class BundleEndToEndTests
{
    private string _projectDir = null!;
    private string _bundlePath = null!;

    [OneTimeSetUp]
    public async Task BuildBundle()
    {
        _projectDir = Directory.CreateTempSubdirectory("trimmer_e2e_").FullName;

        File.WriteAllText(Path.Combine(_projectDir, "shared.cs"),
            "namespace App; public static class Greeter { public static string Hello(string n) => $\"Hello, {n}!\"; }");

        File.WriteAllText(Path.Combine(_projectDir, "index.cshtml"),
            """
            @using App
            @code { string Name => "World"; }
            <html><body><h1>@Greeter.Hello(Name)</h1></body></html>
            """);

        File.WriteAllText(Path.Combine(_projectDir, "about.cshtml"),
            "<html><body><p>@(40 + 2)</p></body></html>");

        Directory.CreateDirectory(Path.Combine(_projectDir, "assets"));
        File.WriteAllText(Path.Combine(_projectDir, "assets", "data.txt"), "static-asset-body");

        var builder = new ProjectBuilder(new FakePackageResolver());
        _bundlePath = await builder.BuildAsync(_projectDir, Path.Combine(_projectDir, "app.trmr"));
    }

    [OneTimeTearDown]
    public void Cleanup() => Directory.Delete(_projectDir, recursive: true);

    private static async Task<(int Status, string Body)> RequestAsync(BundleRequestHandler handler, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var body = new MemoryStream();
        context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(body));

        await handler.HandleAsync(context);

        body.Position = 0;
        using var reader = new StreamReader(body);
        return (context.Response.StatusCode, await reader.ReadToEndAsync());
    }

    [Test]
    public void Bundle_file_is_created()
    {
        Assert.That(File.Exists(_bundlePath), Is.True);
        Assert.That(new FileInfo(_bundlePath).Length, Is.GreaterThan(0));
    }

    [Test]
    public async Task Serves_index_with_code_and_companion_class()
    {
        using var bundle = LoadedBundle.Load(_bundlePath);
        var handler = new BundleRequestHandler(bundle);

        var (status, body) = await RequestAsync(handler, "/");
        Assert.That(status, Is.EqualTo(200));
        Assert.That(body, Does.Contain("Hello, World!"));
    }

    [Test]
    public async Task Serves_secondary_route()
    {
        using var bundle = LoadedBundle.Load(_bundlePath);
        var handler = new BundleRequestHandler(bundle);

        var (_, body) = await RequestAsync(handler, "/about");
        Assert.That(body, Does.Contain("42"));
    }

    [Test]
    public async Task Serves_static_asset()
    {
        using var bundle = LoadedBundle.Load(_bundlePath);
        var handler = new BundleRequestHandler(bundle);

        var (status, body) = await RequestAsync(handler, "/assets/data.txt");
        Assert.That(status, Is.EqualTo(200));
        Assert.That(body, Is.EqualTo("static-asset-body"));
    }

    [Test]
    public async Task Unknown_route_returns_404()
    {
        using var bundle = LoadedBundle.Load(_bundlePath);
        var handler = new BundleRequestHandler(bundle);

        var (status, _) = await RequestAsync(handler, "/does-not-exist");
        Assert.That(status, Is.EqualTo(404));
    }
}
