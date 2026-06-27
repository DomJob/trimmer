namespace Trimmer.Cli;

public enum CommandKind
{
    Help,
    Serve,
    Build,
    Run,
}

/// <summary>A parsed command line invocation.</summary>
public sealed record CliCommand
{
    public CommandKind Kind { get; init; }

    /// <summary>Project directory (serve/build) or bundle path (run).</summary>
    public string Path { get; init; } = ".";

    public int Port { get; init; } = DefaultPort;

    /// <summary>Output bundle path for <c>build</c>.</summary>
    public string? Output { get; init; }

    /// <summary>Set when parsing failed; the message to show the user.</summary>
    public string? Error { get; init; }

    public const int DefaultPort = 5122;

    public static CliCommand Fail(string error) => new() { Kind = CommandKind.Help, Error = error };
}

/// <summary>Parses Trimmer's command line arguments.</summary>
public static class CliParser
{
    public static CliCommand Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CliCommand { Kind = CommandKind.Help };
        }

        var verb = args[0].ToLowerInvariant();
        var rest = args.Skip(1).ToArray();

        return verb switch
        {
            "serve" => ParseServe(rest),
            "build" => ParseBuild(rest),
            "run" => ParseRun(rest),
            "help" or "--help" or "-h" => new CliCommand { Kind = CommandKind.Help },
            _ => CliCommand.Fail($"Unknown command '{args[0]}'."),
        };
    }

    private static CliCommand ParseServe(string[] args)
    {
        if (!TryReadCommon(args, out var path, out var port, out _, out var error, allowOutput: false))
        {
            return CliCommand.Fail(error!);
        }

        return new CliCommand { Kind = CommandKind.Serve, Path = path ?? ".", Port = port };
    }

    private static CliCommand ParseBuild(string[] args)
    {
        if (!TryReadCommon(args, out var path, out var port, out var output, out var error, allowOutput: true))
        {
            return CliCommand.Fail(error!);
        }

        return new CliCommand { Kind = CommandKind.Build, Path = path ?? ".", Port = port, Output = output };
    }

    private static CliCommand ParseRun(string[] args)
    {
        if (!TryReadCommon(args, out var path, out var port, out _, out var error, allowOutput: false))
        {
            return CliCommand.Fail(error!);
        }

        if (path is null)
        {
            return CliCommand.Fail("'run' requires a bundle path, e.g. 'trimmer run app.trmr'.");
        }

        return new CliCommand { Kind = CommandKind.Run, Path = path, Port = port };
    }

    private static bool TryReadCommon(
        string[] args,
        out string? path,
        out int port,
        out string? output,
        out string? error,
        bool allowOutput)
    {
        path = null;
        port = CliCommand.DefaultPort;
        output = null;
        error = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--port" or "-p":
                    if (i + 1 >= args.Length || !int.TryParse(args[++i], out port) || port is < 1 or > 65535)
                    {
                        error = "--port requires a number between 1 and 65535.";
                        return false;
                    }

                    break;

                case "--output" or "-o" when allowOutput:
                    if (i + 1 >= args.Length)
                    {
                        error = "--output requires a file path.";
                        return false;
                    }

                    output = args[++i];
                    break;

                default:
                    if (arg.StartsWith('-'))
                    {
                        error = $"Unknown option '{arg}'.";
                        return false;
                    }

                    if (path is not null)
                    {
                        error = $"Unexpected argument '{arg}'.";
                        return false;
                    }

                    path = arg;
                    break;
            }
        }

        return true;
    }
}
