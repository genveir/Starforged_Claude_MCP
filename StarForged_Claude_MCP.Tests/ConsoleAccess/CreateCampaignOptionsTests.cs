using FluentAssertions;
using StarForged_Claude_MCP.ConsoleAccess.CreateCampaign;

namespace StarForged_Claude_MCP.Tests.ConsoleAccess;

public class CreateCampaignOptionsTests
{
    [Fact]
    public void Parse_WithOneName_ShouldTrimAndReturnTheName()
    {
        CreateCampaignOptions.Parse(["  My Campaign  "])
            .Should().Be(new CreateCampaignOptions("My Campaign"));
    }

    [Theory]
    [InlineData()]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("first", "second")]
    public void Parse_WithoutExactlyOneNonEmptyName_ShouldReturnNull(params string[] args)
    {
        CreateCampaignOptions.Parse(args).Should().BeNull();
    }
}