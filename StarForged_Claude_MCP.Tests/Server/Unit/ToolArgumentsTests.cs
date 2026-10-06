using FluentAssertions;
using StarForged_Claude_MCP.Server.Tools;

namespace StarForged_Claude_MCP.Tests.Server.Unit;

public class ToolArgumentsTests
{
    [Fact]
    public void RequireCampaign_WithAnUnpaddedName_ShouldReturnTheName()
    {
        var arguments = new ToolArguments(new Dictionary<string, object> { ["campaign"] = "Starforged" });

        arguments.RequireCampaign().Value.Should().Be("Starforged");
    }

    [Theory]
    [InlineData(" Starforged")]
    [InlineData("Starforged ")]
    public void RequireCampaign_WithPadding_ShouldRefuseTheName(string campaign)
    {
        var arguments = new ToolArguments(new Dictionary<string, object> { ["campaign"] = campaign });

        arguments.Invoking(a => a.RequireCampaign()).Should().Throw<ArgumentException>()
            .WithMessage("*cannot start or end with a space*");
    }

    [Fact]
    public void RequireStateId_WithAWellFormedId_ShouldReturnTheId()
    {
        var arguments = new ToolArguments(new Dictionary<string, object> { ["stateId"] = "character-1.progress" });

        arguments.RequireStateId("stateId").Value.Should().Be("character-1.progress");
    }

    [Theory]
    [InlineData("character.progress.extra")]
    [InlineData("character/progress")]
    public void RequireStateId_WithAMalformedId_ShouldRefuseTheId(string stateTrackingId)
    {
        var arguments = new ToolArguments(new Dictionary<string, object> { ["stateId"] = stateTrackingId });

        arguments.Invoking(a => a.RequireStateId("stateId")).Should().Throw<ArgumentException>()
            .WithMessage("*not well-formed*");
    }
}