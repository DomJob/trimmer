using Trimmer.Rendering;

namespace Trimmer.Tests;

public class RazorRenderingTests
{
    private static string Render(string cshtml, IEnumerable<string>? csharp = null)
    {
        var page = RazorPageCompiler.Compile(cshtml, csharp).CreateInstance();
        return page.RenderAsync().GetAwaiter().GetResult();
    }

    [Test]
    public void Renders_plain_html()
    {
        var html = Render("<h1>Hello</h1>");
        Assert.That(html, Is.EqualTo("<h1>Hello</h1>"));
    }

    [Test]
    public void Evaluates_inline_csharp_expression()
    {
        var html = Render("<p>@(1 + 2)</p>");
        Assert.That(html, Does.Contain("<p>3</p>"));
    }

    [Test]
    public void Html_encodes_expression_output()
    {
        var html = Render("<p>@(\"<b>\")</p>");
        Assert.That(html, Does.Contain("&lt;b&gt;"));
    }

    [Test]
    public void Raw_output_is_not_encoded()
    {
        var html = Render("<p>@Raw(\"<b>x</b>\")</p>");
        Assert.That(html, Does.Contain("<b>x</b>"));
    }

    [Test]
    public void Supports_code_block_with_members()
    {
        const string cshtml = """
            @code {
                int Square(int n) => n * n;
            }
            <p>@Square(4)</p>
            """;
        var html = Render(cshtml);
        Assert.That(html, Does.Contain("<p>16</p>"));
    }

    [Test]
    public void Supports_functions_block()
    {
        const string cshtml = """
            @functions {
                string Greeting => "hi";
            }
            <p>@Greeting</p>
            """;
        var html = Render(cshtml);
        Assert.That(html, Does.Contain("<p>hi</p>"));
    }

    [Test]
    public void Can_use_types_from_companion_cs_file()
    {
        const string csharp = """
            namespace App;
            public static class Calc { public static int Add(int a, int b) => a + b; }
            """;
        const string cshtml = """
            @using App
            <p>@Calc.Add(2, 5)</p>
            """;
        var html = Render(cshtml, [csharp]);
        Assert.That(html, Does.Contain("<p>7</p>"));
    }

    [Test]
    public void Supports_loops_and_conditionals()
    {
        const string cshtml = """
            <ul>
            @for (var i = 1; i <= 3; i++)
            {
                <li>Item @i</li>
            }
            </ul>
            """;
        var html = Render(cshtml);
        Assert.That(html, Does.Contain("<li>Item 1</li>"));
        Assert.That(html, Does.Contain("<li>Item 3</li>"));
    }
}
