using FluentAssertions;
using StarForged_Claude_MCP.Ironsworn.DomainTypes;
using StarForged_Claude_MCP.Ironsworn.Errors;
using StarForged_Claude_MCP.Shared.Results;

namespace StarForged_Claude_MCP.Tests.Ironsworn;

public class TrackIdTests
{
    [Theory]
    [InlineData("vow.handle-the-plantation", TrackKind.Vow, "handle-the-plantation")]
    [InlineData("connection.jorran-hasfer", TrackKind.Connection, "jorran-hasfer")]
    [InlineData("expedition.the-sundered-belt", TrackKind.Expedition, "the-sundered-belt")]
    [InlineData("combat.ambush-at-dock-3", TrackKind.Combat, "ambush-at-dock-3")]
    [InlineData("VOW.Handle-The-Plantation", TrackKind.Vow, "Handle-The-Plantation")]
    public void From_WithAKindAndAName_ShouldSplitThem(string id, TrackKind expectedKind, string expectedName)
    {
        var result = TrackId.From(new StateTrackingId(id));

        result.Should().BeOfType<SuccessResult<TrackId, ErrorCode>>()
            .Which.Value.Should().Be(new TrackId(expectedKind, expectedName));
    }

    [Fact]
    public void From_WithoutADot_ShouldFailWithTrackIdNeedsKind()
    {
        var result = TrackId.From(new StateTrackingId("handle-the-plantation"));

        result.Should().BeOfType<FailureResult<TrackId, ErrorCode>>()
            .Which.Error.Should().Be(ErrorCode.Track_Id_Needs_Kind);
    }

    [Theory]
    [InlineData("quest.handle-the-plantation")]
    [InlineData("1.handle-the-plantation")]
    public void From_WithAPrefixThatIsNoKind_ShouldFailWithTrackKindUnknown(string id)
    {
        var result = TrackId.From(new StateTrackingId(id));

        result.Should().BeOfType<FailureResult<TrackId, ErrorCode>>()
            .Which.Error.Should().Be(ErrorCode.Track_Kind_Unknown);
    }

    [Fact]
    public void Value_ShouldJoinTheKindInLowercaseAndTheNameAsGiven()
    {
        new TrackId(TrackKind.Expedition, "The-Sundered-Belt").Value.Should().Be("expedition.The-Sundered-Belt");
    }
}
