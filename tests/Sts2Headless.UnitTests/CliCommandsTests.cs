using Sts2Headless.Commands;
using Xunit;

namespace Sts2Headless.UnitTests;

// The CLI dispatch table replaced a hand-rolled `args.Contains("--probe-x")`
// ladder in Program.cs. These tests pin the registry's behaviour so a future
// edit can't silently drop a verb, break an alias, or let --stdio (the product
// path, deliberately handled outside the table) leak into the diagnostics.
public class CliCommandsTests
{
    [Theory]
    [InlineData("--inspect-sts2")]
    [InlineData("--probe-init")]
    [InlineData("--probe-bootstrap")]
    [InlineData("--probe-combat-stall")]
    [InlineData("--generate-content-ids")]
    [InlineData("--rebuild-replay-index")]
    [InlineData("--list-members")]
    public void Match_resolves_a_known_verb(string verb)
    {
        var cmd = CliCommands.Match([verb]);
        Assert.NotNull(cmd);
        Assert.Contains(verb, cmd!.Verbs);
    }

    [Fact]
    public void Match_finds_the_verb_anywhere_in_args()
    {
        // Position-agnostic, matching the historical args.Contains behaviour.
        var cmd = CliCommands.Match(["--some-flag", "value", "--probe-types", "Godot.OS"]);
        Assert.NotNull(cmd);
        Assert.Contains("--probe-types", cmd!.Verbs);
    }

    [Fact]
    public void GenerateContentIds_alias_resolves_to_the_same_command()
    {
        var canonical = CliCommands.Match(["--generate-content-ids"]);
        var alias = CliCommands.Match(["--generate-card-ids"]);
        Assert.NotNull(canonical);
        Assert.Same(canonical, alias);
    }

    [Fact]
    public void Match_returns_null_when_no_verb_present()
    {
        Assert.Null(CliCommands.Match([]));
        Assert.Null(CliCommands.Match(["--unknown"]));
        // --help is handled by Program.cs directly, not the table.
        Assert.Null(CliCommands.Match(["--help"]));
    }

    [Fact]
    public void Stdio_is_not_a_table_verb()
    {
        // --stdio owns the bootstrap/binding lifecycle and is dispatched on its
        // own branch in Program.cs; it must never be a diagnostic command.
        Assert.Null(CliCommands.Match(["--stdio"]));
        Assert.DoesNotContain(CliCommands.All, c => c.Verbs.Contains("--stdio"));
    }

    [Fact]
    public void Every_command_has_a_verb_and_help()
    {
        Assert.NotEmpty(CliCommands.All);
        Assert.All(CliCommands.All, c =>
        {
            Assert.NotEmpty(c.Verbs);
            Assert.All(c.Verbs, v => Assert.StartsWith("--", v));
            Assert.False(string.IsNullOrWhiteSpace(c.Help));
        });
    }

    [Fact]
    public void Verbs_are_unique_across_the_table()
    {
        var verbs = CliCommands.All.SelectMany(c => c.Verbs).ToList();
        Assert.Equal(verbs.Count, verbs.Distinct().Count());
    }

    [Fact]
    public void WriteHelp_lists_every_verb()
    {
        var writer = new StringWriter();
        CliCommands.WriteHelp(writer);
        var text = writer.ToString();
        foreach (var verb in CliCommands.All.SelectMany(c => c.Verbs))
        {
            Assert.Contains(verb, text);
        }
    }
}
