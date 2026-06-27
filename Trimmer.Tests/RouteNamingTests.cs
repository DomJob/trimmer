using Trimmer.Build;

namespace Trimmer.Tests;

public class RouteNamingTests
{
    [TestCase("index.cshtml", "/")]
    [TestCase("about.cshtml", "/about")]
    [TestCase("blog/index.cshtml", "/blog")]
    [TestCase("blog/post.cshtml", "/blog/post")]
    public void RouteForFile(string relative, string expected)
    {
        Assert.That(RouteNaming.RouteForFile(relative.Replace('/', Path.DirectorySeparatorChar)), Is.EqualTo(expected));
    }

    [TestCase("/", "/")]
    [TestCase("/about", "/about")]
    [TestCase("/about.cshtml", "/about")]
    [TestCase("/blog/", "/blog")]
    [TestCase("", "/")]
    public void NormalizeRequest(string input, string expected)
    {
        Assert.That(RouteNaming.NormalizeRequest(input), Is.EqualTo(expected));
    }

    [Test]
    public void ClassName_is_a_valid_identifier()
    {
        Assert.That(RouteNaming.ClassNameForRoute("/blog/post"), Is.EqualTo("Page_blog_post"));
        Assert.That(RouteNaming.ClassNameForRoute("/"), Is.EqualTo("Page"));
    }
}
