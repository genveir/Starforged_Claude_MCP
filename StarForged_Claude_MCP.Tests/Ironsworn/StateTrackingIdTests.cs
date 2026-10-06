using FluentAssertions;
using StarForged_Claude_MCP.Ironsworn.DomainTypes;

namespace StarForged_Claude_MCP.Tests.Database;

public class StateTrackingIdTests
{
    [Theory]
    [InlineData("character")]
    [InlineData("character-1")]
    [InlineData("character-1.progress")]
    public void IsWellFormed_WithLettersDigitsHyphensAndOneDot_ShouldBeTrue(string stateTrackingId)
    {
        StateTrackingId.IsWellFormed(stateTrackingId).Should().BeTrue();
    }

    [Theory]
    [InlineData("character.progress.extra")]
    [InlineData("character/progress")]
    [InlineData("character progress")]
    [InlineData("character.")]
    public void IsWellFormed_WithInvalidCharactersOrDotPlacement_ShouldBeFalse(string stateTrackingId)
    {
        StateTrackingId.IsWellFormed(stateTrackingId).Should().BeFalse();
    }
}