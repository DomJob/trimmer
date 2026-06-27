using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace Trimmer.Rendering;

/// <summary>Compiles C# source files into an in-memory assembly with Roslyn.</summary>
public static class RoslynCompiler
{
    /// <summary>Compiles the supplied sources, loads the assembly and returns it.</summary>
    public static Assembly CompileToAssembly(
        string assemblyName,
        IEnumerable<string> sources,
        IReadOnlyList<MetadataReference> references)
    {
        using var stream = Emit(assemblyName, sources, references);
        return Assembly.Load(stream.ToArray());
    }

    /// <summary>Compiles the supplied sources and writes the assembly bytes to <paramref name="outputPath"/>.</summary>
    public static void CompileToFile(
        string assemblyName,
        IEnumerable<string> sources,
        IReadOnlyList<MetadataReference> references,
        string outputPath)
    {
        using var stream = Emit(assemblyName, sources, references);
        using var file = File.Create(outputPath);
        stream.Position = 0;
        stream.CopyTo(file);
    }

    private static MemoryStream Emit(
        string assemblyName,
        IEnumerable<string> sources,
        IReadOnlyList<MetadataReference> references)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTrees = sources
            .Select(source => CSharpSyntaxTree.ParseText(source, parseOptions))
            .ToList();

        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Annotations));

        var stream = new MemoryStream();
        EmitResult result = compilation.Emit(stream);
        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString())
                .ToList();
            stream.Dispose();
            throw new CompilationException(errors);
        }

        stream.Position = 0;
        return stream;
    }
}

/// <summary>Thrown when Roslyn reports compilation errors.</summary>
public sealed class CompilationException(IReadOnlyList<string> errors)
    : Exception("Compilation failed:\n" + string.Join("\n", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
