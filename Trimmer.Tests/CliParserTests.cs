using Trimmer.Cli;

namespace Trimmer.Tests;

public class CliParserTests
{
    [Test]
    public void No_args_shows_help()
    {
        Assert.That(CliParser.Parse([]).Kind, Is.EqualTo(CommandKind.Help));
    }

    [Test]
    public void Serve_defaults()
    {
        var command = CliParser.Parse(["serve"]);
        Assert.That(command.Kind, Is.EqualTo(CommandKind.Serve));
        Assert.That(command.Path, Is.EqualTo("."));
        Assert.That(command.Port, Is.EqualTo(5122));
    }

    [Test]
    public void Serve_with_directory_and_port()
    {
        var command = CliParser.Parse(["serve", "./www", "--port", "8080"]);
        Assert.That(command.Path, Is.EqualTo("./www"));
        Assert.That(command.Port, Is.EqualTo(8080));
    }

    [Test]
    public void Build_with_output()
    {
        var command = CliParser.Parse(["build", "src", "-o", "out.trmr"]);
        Assert.That(command.Kind, Is.EqualTo(CommandKind.Build));
        Assert.That(command.Path, Is.EqualTo("src"));
        Assert.That(command.Output, Is.EqualTo("out.trmr"));
    }

    [Test]
    public void Run_requires_bundle_path()
    {
        Assert.That(CliParser.Parse(["run"]).Error, Is.Not.Null);
    }

    [Test]
    public void Run_with_bundle_and_port()
    {
        var command = CliParser.Parse(["run", "app.trmr", "-p", "9000"]);
        Assert.That(command.Kind, Is.EqualTo(CommandKind.Run));
        Assert.That(command.Path, Is.EqualTo("app.trmr"));
        Assert.That(command.Port, Is.EqualTo(9000));
    }

    [Test]
    public void Unknown_command_is_an_error()
    {
        Assert.That(CliParser.Parse(["frobnicate"]).Error, Is.Not.Null);
    }

    [Test]
    public void Invalid_port_is_an_error()
    {
        Assert.That(CliParser.Parse(["serve", "--port", "abc"]).Error, Is.Not.Null);
        Assert.That(CliParser.Parse(["serve", "--port", "99999"]).Error, Is.Not.Null);
    }

    [Test]
    public void Unknown_option_is_an_error()
    {
        Assert.That(CliParser.Parse(["serve", "--frob"]).Error, Is.Not.Null);
    }

    [Test]
    public void Output_not_allowed_for_serve()
    {
        Assert.That(CliParser.Parse(["serve", "-o", "x"]).Error, Is.Not.Null);
    }
}
