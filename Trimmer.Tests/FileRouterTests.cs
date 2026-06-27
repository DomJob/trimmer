using Trimmer.Hosting;

namespace Trimmer.Tests;

public class FileRouterTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Directory.CreateTempSubdirectory("trimmer_router_").FullName;
        File.WriteAllText(Path.Combine(_root, "index.cshtml"), "<h1>home</h1>");
        File.WriteAllText(Path.Combine(_root, "login.cshtml"), "<h1>login</h1>");
        File.WriteAllText(Path.Combine(_root, "script.cs"), "class C {}");
        Directory.CreateDirectory(Path.Combine(_root, "assets"));
        File.WriteAllText(Path.Combine(_root, "assets", "app.js"), "console.log(1)");
        Directory.CreateDirectory(Path.Combine(_root, "blog"));
        File.WriteAllText(Path.Combine(_root, "blog", "index.cshtml"), "<h1>blog</h1>");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    private FileRouter Router => new(_root);

    [Test]
    public void Root_maps_to_index()
    {
        var result = Router.Resolve("/");
        Assert.That(result.Kind, Is.EqualTo(RouteKind.Razor));
        Assert.That(result.PhysicalPath, Does.EndWith("index.cshtml"));
    }

    [Test]
    public void Extensionless_path_maps_to_cshtml()
    {
        var result = Router.Resolve("/login");
        Assert.That(result.Kind, Is.EqualTo(RouteKind.Razor));
        Assert.That(result.PhysicalPath, Does.EndWith("login.cshtml"));
    }

    [Test]
    public void Explicit_cshtml_is_razor()
    {
        Assert.That(Router.Resolve("/login.cshtml").Kind, Is.EqualTo(RouteKind.Razor));
    }

    [Test]
    public void Static_asset_is_served()
    {
        var result = Router.Resolve("/assets/app.js");
        Assert.That(result.Kind, Is.EqualTo(RouteKind.Static));
        Assert.That(result.PhysicalPath, Does.EndWith("app.js"));
    }

    [Test]
    public void Cs_files_are_forbidden()
    {
        Assert.That(Router.Resolve("/script.cs").Kind, Is.EqualTo(RouteKind.Forbidden));
    }

    [Test]
    public void Directory_serves_its_index()
    {
        Assert.That(Router.Resolve("/blog").Kind, Is.EqualTo(RouteKind.Razor));
        Assert.That(Router.Resolve("/blog/").Kind, Is.EqualTo(RouteKind.Razor));
    }

    [Test]
    public void Missing_path_is_not_found()
    {
        Assert.That(Router.Resolve("/nope").Kind, Is.EqualTo(RouteKind.NotFound));
    }

    [Test]
    public void Directory_traversal_is_blocked()
    {
        Assert.That(Router.Resolve("/../../etc/passwd").Kind, Is.EqualTo(RouteKind.Forbidden));
    }
}
