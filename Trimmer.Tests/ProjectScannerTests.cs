using Trimmer.Build;

namespace Trimmer.Tests;

public class ProjectScannerTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Directory.CreateTempSubdirectory("trimmer_scan_").FullName;
        File.WriteAllText(Path.Combine(_root, "a.cs"), "#:package Humanizer@2.14.1\nnamespace App; public class A {}");
        File.WriteAllText(Path.Combine(_root, "b.cs"), "namespace App; public class B {}");
        File.WriteAllText(Path.Combine(_root, "index.cshtml"), "<h1>x</h1>");

        Directory.CreateDirectory(Path.Combine(_root, "obj"));
        File.WriteAllText(Path.Combine(_root, "obj", "generated.cs"), "class ShouldBeIgnored {}");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void LoadCSharp_strips_package_directives()
    {
        var project = ProjectScanner.LoadCSharp(_root);
        Assert.That(project.CSharpSources, Has.Count.EqualTo(2));
        Assert.That(project.CSharpSources.Any(s => s.Contains("#:package")), Is.False);
    }

    [Test]
    public void LoadCSharp_collects_packages()
    {
        var project = ProjectScanner.LoadCSharp(_root);
        Assert.That(project.Packages, Has.Count.EqualTo(1));
        Assert.That(project.Packages[0].Name, Is.EqualTo("Humanizer"));
    }

    [Test]
    public void Enumeration_ignores_obj_directory()
    {
        var files = ProjectScanner.EnumerateCSharpFiles(_root).ToList();
        Assert.That(files.Any(f => f.Contains("generated.cs")), Is.False);
        Assert.That(files, Has.Count.EqualTo(2));
    }

    [Test]
    public void Enumerates_razor_files()
    {
        Assert.That(ProjectScanner.EnumerateRazorFiles(_root).Count(), Is.EqualTo(1));
    }
}
