using Trimmer.Build;
using Trimmer.Cli;
using Trimmer.Hosting;
using Trimmer.Packages;

var command = CliParser.Parse(args);

if (command.Error is not null)
{
    Console.Error.WriteLine($"error: {command.Error}");
    Console.Error.WriteLine();
    HelpText.Print(Console.Error);
    return 1;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    switch (command.Kind)
    {
        case CommandKind.Serve:
        {
            var resolver = new NuGetPackageResolver();
            await TrimmerServer.ServeAsync(command.Path, command.Port, resolver, cts.Token);
            break;
        }

        case CommandKind.Build:
        {
            var resolver = new NuGetPackageResolver();
            var builder = new ProjectBuilder(resolver);
            Console.WriteLine($"Building {Path.GetFullPath(command.Path)} ...");
            var output = await builder.BuildAsync(command.Path, command.Output, cts.Token);
            Console.WriteLine($"Built bundle: {output}");
            Console.WriteLine($"Run it with: trimmer run {Path.GetFileName(output)}");
            break;
        }

        case CommandKind.Run:
        {
            await BundleServer.RunAsync(command.Path, command.Port, cts.Token);
            break;
        }

        default:
        case CommandKind.Help:
            HelpText.Print(Console.Out);
            break;
    }

    return 0;
}
catch (OperationCanceledException)
{
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 1;
}
