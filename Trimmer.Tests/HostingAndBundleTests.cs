using Trimmer.Build;
using Trimmer.Hosting;
using Trimmer.Packages;

namespace Trimmer.Tests;

public class LiveReloadServiceTests
{
    [Test]
    public void Injects_script_before_body_close()
    {
        var service = new LiveReloadService();
        var html = service.InjectInto("<html><body><h1>x</h1></body></html>");

        Assert.That(html, Does.Contain(LiveReloadService.Endpoint));
        Assert.That(html.IndexOf("EventSource", StringComparison.Ordinal),
            Is.LessThan(html.IndexOf("</body>", StringComparison.Ordinal)));
    }

    [Test]
    public void Appends_script_when_no_body_tag()
    {
        var service = new LiveReloadService();
        var html = service.InjectInto("<h1>x</h1>");
        Assert.That(html, Does.StartWith("<h1>x</h1>"));
        Assert.That(html, Does.Contain("EventSource"));
    }

    [Test]
    public async Task NotifyChange_releases_waiters()
    {
        var service = new LiveReloadService();
        var wait = service.WaitForChangeAsync(CancellationToken.None);
        Assert.That(wait.IsCompleted, Is.False);

        service.NotifyChange();
        await wait.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(wait.IsCompletedSuccessfully, Is.True);
    }
}

public class BundleManifestTests
{
    [Test]
    public void Round_trips_through_json()
    {
        var manifest = new BundleManifest
        {
            Assembly = "pages.dll",
            Routes = { ["/"] = "TrimmerGenerated.Page", ["/about"] = "TrimmerGenerated.Page_about" },
            Libraries = { "Humanizer.dll" }
        };

        var restored = BundleManifest.FromJson(manifest.ToJson());

        Assert.That(restored.Assembly, Is.EqualTo("pages.dll"));
        Assert.That(restored.Routes["/about"], Is.EqualTo("TrimmerGenerated.Page_about"));
        Assert.That(restored.Libraries, Does.Contain("Humanizer.dll"));
    }
}

public class NuGetPackageResolverTests
{
    [Test]
    public async Task Empty_package_set_resolves_to_nothing()
    {
        var resolver = new NuGetPackageResolver();
        var result = await resolver.ResolveAsync([]);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Cache_key_is_order_independent()
    {
        var a = NuGetPackageResolver.ComputeCacheKey([new PackageReference("A", "1.0"), new PackageReference("B", "2.0")]);
        var b = NuGetPackageResolver.ComputeCacheKey([new PackageReference("B", "2.0"), new PackageReference("A", "1.0")]);
        Assert.That(a, Is.EqualTo(b));
    }

    [Test]
    public void Cache_key_changes_with_version()
    {
        var a = NuGetPackageResolver.ComputeCacheKey([new PackageReference("A", "1.0")]);
        var b = NuGetPackageResolver.ComputeCacheKey([new PackageReference("A", "2.0")]);
        Assert.That(a, Is.Not.EqualTo(b));
    }

    [Test]
    public void Probe_project_lists_package_references()
    {
        var resolver = new NuGetPackageResolver();
        var xml = resolver.BuildProbeProjectXml([new PackageReference("Humanizer", "2.14.1"), new PackageReference("Dapper", null)]);

        Assert.That(xml, Does.Contain("<PackageReference Include=\"Humanizer\" Version=\"2.14.1\" />"));
        Assert.That(xml, Does.Contain("<PackageReference Include=\"Dapper\" Version=\"*\" />"));
        Assert.That(xml, Does.Contain("net10.0"));
    }
}
