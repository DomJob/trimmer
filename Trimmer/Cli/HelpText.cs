namespace Trimmer.Cli;

/// <summary>Renders the CLI help banner.</summary>
public static class HelpText
{
    public static void Print(TextWriter writer)
    {
        writer.WriteLine(
            """
            USAGE:
              trimmer serve [dir] [--port <n>]      Serve a project with hot reload (default port 5122).
              trimmer build [dir] [--output <file>] Compile a project into a compact .trm bundle.
              trimmer run <bundle> [--port <n>]     Serve a pre-built bundle (production).
              trimmer help                          Show this help.

            EXAMPLES:
              trimmer serve
              trimmer serve ./www --port 8080
              trimmer build ./www --output site.trm
              trimmer run site.trm

            Drop .cshtml and .cs files in a folder - no .csproj, .sln or appsettings.json required.
            Reference NuGet packages inside any file with:  #:package Humanizer@2.14.1
            """);
    }
}
