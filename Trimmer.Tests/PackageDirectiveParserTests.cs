using Trimmer.Packages;

namespace Trimmer.Tests;

public class PackageDirectiveParserTests
{
    [Test]
    public void Parses_name_and_version()
    {
        var result = PackageDirectiveParser.Scan("#:package Humanizer@2.14.1\n<h1>hi</h1>");

        Assert.That(result.Packages, Has.Count.EqualTo(1));
        Assert.That(result.Packages[0].Name, Is.EqualTo("Humanizer"));
        Assert.That(result.Packages[0].Version, Is.EqualTo("2.14.1"));
    }

    [Test]
    public void Parses_name_without_version()
    {
        var result = PackageDirectiveParser.Scan("#:package Newtonsoft.Json");

        Assert.That(result.Packages[0].Name, Is.EqualTo("Newtonsoft.Json"));
        Assert.That(result.Packages[0].Version, Is.Null);
    }

    [Test]
    public void Strips_directive_lines_from_content()
    {
        var result = PackageDirectiveParser.Scan("#:package A@1.0.0\nhello\n#:package B\nworld");

        Assert.That(result.Content, Does.Not.Contain("#:package"));
        Assert.That(result.Content, Does.Contain("hello"));
        Assert.That(result.Content, Does.Contain("world"));
    }

    [Test]
    public void Preserves_line_count_for_diagnostics()
    {
        var result = PackageDirectiveParser.Scan("#:package A\nline2\nline3");
        Assert.That(result.Content.Split('\n'), Has.Length.EqualTo(3));
    }

    [Test]
    public void Deduplicates_within_a_file()
    {
        var result = PackageDirectiveParser.Scan("#:package A@1\n#:package a@2");
        Assert.That(result.Packages, Has.Count.EqualTo(1));
    }

    [Test]
    public void Ignores_non_directive_hash_lines()
    {
        var result = PackageDirectiveParser.Scan("# heading\n## sub\n#:package A");
        Assert.That(result.Packages, Has.Count.EqualTo(1));
        Assert.That(result.Content, Does.Contain("# heading"));
    }

    [Test]
    public void Collect_merges_across_sources()
    {
        var packages = PackageDirectiveParser.Collect(
        [
            "#:package A@1.0.0",
            "#:package B@2.0.0",
            "#:package A@3.0.0",
        ]);

        Assert.That(packages, Has.Count.EqualTo(2));
        Assert.That(packages.First(p => p.Name == "A").Version, Is.EqualTo("1.0.0"));
    }
}
