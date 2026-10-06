using FluentAssertions;
using StarForged_Claude_MCP.ConsoleAccess.Checkpoints;

namespace StarForged_Claude_MCP.Tests.ConsoleAccess;

public class RestoreCheckpointOptionsTests
{
    [Fact]
    public void Parse_WithACampaignAndAName_ShouldReturnBoth_AndAsk()
    {
        var options = RestoreCheckpointOptions.Parse(["Iron Expanse", "session-7"]);

        options.Should().NotBeNull();
        options.Campaign.Value.Should().Be("Iron Expanse");
        options.Name.Value.Should().Be("session-7");
        options.Yes.Should().BeFalse();
    }

    [Theory]
    [InlineData("--yes")]
    [InlineData("-y")]
    public void Parse_WithYes_ShouldNotAsk(string yes)
    {
        RestoreCheckpointOptions.Parse(["Iron Expanse", "session-7", yes])!.Yes.Should().BeTrue();
    }

    [Theory]
    [InlineData()]
    [InlineData("Iron Expanse")]
    [InlineData("Iron Expanse", "session 7")]
    [InlineData(" Iron Expanse", "session-7")]
    [InlineData("", "session-7")]
    [InlineData("--yes", "Iron Expanse", "session-7")]
    [InlineData("Iron Expanse", "session-7", "--force")]
    public void Parse_WithoutAWellFormedCampaignAndName_OrWithAnUnknownArgument_ShouldReturnNull(params string[] args)
    {
        RestoreCheckpointOptions.Parse(args).Should().BeNull();
    }
}
