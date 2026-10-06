using FluentAssertions;
using StarForged_Claude_MCP.ConsoleAccess.Checkpoints;

namespace StarForged_Claude_MCP.Tests.ConsoleAccess;

public class CreateCheckpointOptionsTests
{
    [Fact]
    public void Parse_WithACampaignAndAName_ShouldReturnBoth()
    {
        var options = CreateCheckpointOptions.Parse(["Iron Expanse", "session-7"]);

        options.Should().NotBeNull();
        options.Campaign.Value.Should().Be("Iron Expanse");
        options.Name.Value.Should().Be("session-7");
    }

    [Theory]
    [InlineData()]
    [InlineData("Iron Expanse")]
    [InlineData("Iron Expanse", "session 7")]
    [InlineData(" Iron Expanse", "session-7")]
    [InlineData("", "session-7")]
    [InlineData("Iron Expanse", "--yes")]
    [InlineData("Iron Expanse", "session-7", "--yes")]
    public void Parse_WithoutAWellFormedCampaignAndNameAlone_ShouldReturnNull(params string[] args)
    {
        CreateCheckpointOptions.Parse(args).Should().BeNull();
    }
}
