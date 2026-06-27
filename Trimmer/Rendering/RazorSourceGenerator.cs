using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Extensions;

namespace Trimmer.Rendering;

/// <summary>
/// Turns a <c>.cshtml</c> document into generated C# source using the Razor language engine.
/// The generated class derives from <see cref="TrimmerPage"/>.
/// </summary>
public static partial class RazorSourceGenerator
{
    public const string GeneratedNamespace = "TrimmerGenerated";

    [GeneratedRegex(@"@code(?=\s*\{)")]
    private static partial Regex CodeBlock();

    /// <summary>Generates C# source for a single page.</summary>
    /// <param name="cshtml">The (already package-stripped) Razor markup.</param>
    /// <param name="className">The deterministic class name to emit.</param>
    public static string Generate(string cshtml, string className)
    {
        ArgumentNullException.ThrowIfNull(cshtml);
        ArgumentException.ThrowIfNullOrEmpty(className);

        // Razor uses @functions for class-level members; treat Blazor-style @code the same way.
        var markup = CodeBlock().Replace(cshtml, "@functions");

        var fileSystem = RazorProjectFileSystem.Create(Directory.GetCurrentDirectory());
        var engine = RazorProjectEngine.Create(RazorConfiguration.Default, fileSystem, builder =>
        {
            builder.SetNamespace(GeneratedNamespace);
            builder.SetBaseType("global::Trimmer.Rendering.TrimmerPage");

            FunctionsDirective.Register(builder);
            InheritsDirective.Register(builder);

            builder.ConfigureClass((document, node) =>
            {
                node.ClassName = className;
                node.Modifiers.Clear();
                node.Modifiers.Add("public");
            });

            builder.AddDefaultImports(
                "@using System",
                "@using System.Collections.Generic",
                "@using System.Linq",
                "@using System.Threading.Tasks");
        });

        var sourceDocument = RazorSourceDocument.Create(markup, $"{className}.cshtml");
        var codeDocument = engine.Process(
            sourceDocument,
            fileKind: FileKinds.Legacy,
            importSources: [],
            tagHelpers: []);

        var csharp = codeDocument.GetCSharpDocument();
        var diagnostics = csharp.Diagnostics
            .Where(d => d.Severity == RazorDiagnosticSeverity.Error)
            .ToList();

        if (diagnostics.Count > 0)
        {
            throw new RazorGenerationException(diagnostics.Select(d => d.ToString()).ToList());
        }

        return csharp.GeneratedCode;
    }
}

/// <summary>Thrown when Razor reports parse errors for a page.</summary>
public sealed class RazorGenerationException(IReadOnlyList<string> errors)
    : Exception("Razor generation failed:\n" + string.Join("\n", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
