using FluentAssertions;
using StarForged_Claude_MCP.Ironsworn.DomainTypes;

namespace StarForged_Claude_MCP.Tests.Ironsworn;

public class TrackTests
{
    private static readonly TrackId Plantation = new(TrackKind.Vow, "handle-the-plantation");
    private const string Description = "Handle the drug plantation.";

    [Fact]
    public void Create_WithoutTicks_ShouldStartAtZero()
    {
        var change = Track.Create(Plantation, Description, Rank.Dangerous);

        change.Track.Id.Should().Be(Plantation);
        change.Track.Description.Should().Be(Description);
        change.Track.Rank.Should().Be(Rank.Dangerous);
        change.Track.Ticks.Should().Be(0);
        change.Clamped.Should().Be(0);
    }

    [Fact]
    public void Create_WithTicksInRange_ShouldKeepThem()
    {
        var change = Track.Create(Plantation, Description, Rank.Dangerous, ticks: 13);

        change.Track.Ticks.Should().Be(13);
        change.Clamped.Should().Be(0);
    }

    [Theory]
    [InlineData(-3, 0, 3)]
    [InlineData(45, 40, 5)]
    public void Create_WithTicksOutOfRange_ShouldClampThem(int ticks, int expectedTicks, int expectedClamped)
    {
        var change = Track.Create(Plantation, Description, Rank.Dangerous, ticks);

        change.Track.Ticks.Should().Be(expectedTicks);
        change.Clamped.Should().Be(expectedClamped);
    }

    [Theory]
    [InlineData(Rank.Troublesome, 12)]
    [InlineData(Rank.Dangerous, 8)]
    [InlineData(Rank.Formidable, 4)]
    [InlineData(Rank.Extreme, 2)]
    [InlineData(Rank.Epic, 1)]
    public void TicksPerMark_ShouldFollowTheRank(Rank rank, int expectedTicks)
    {
        rank.TicksPerMark().Should().Be(expectedTicks);
    }

    [Theory]
    [InlineData(Rank.Troublesome, 1, 12)]
    [InlineData(Rank.Troublesome, 3, 36)]
    [InlineData(Rank.Dangerous, 1, 8)]
    [InlineData(Rank.Dangerous, 2, 16)]
    [InlineData(Rank.Formidable, 1, 4)]
    [InlineData(Rank.Formidable, 3, 12)]
    [InlineData(Rank.Extreme, 1, 2)]
    [InlineData(Rank.Extreme, 5, 10)]
    [InlineData(Rank.Epic, 1, 1)]
    [InlineData(Rank.Epic, 7, 7)]
    public void Mark_ShouldAddTheRanksTicksPerMarkEachTime(Rank rank, int times, int expectedTicks)
    {
        var change = Track.Create(Plantation, Description, rank).Track.Mark(times);

        change.Track.Ticks.Should().Be(expectedTicks);
        change.Clamped.Should().Be(0);
    }

    [Fact]
    public void Mark_WithANegativeNumber_ShouldRemoveProgress()
    {
        var change = Track.Create(Plantation, Description, Rank.Formidable, ticks: 20).Track.Mark(-2);

        change.Track.Ticks.Should().Be(12);
        change.Clamped.Should().Be(0);
    }

    [Theory]
    [InlineData(3, 40, 8)]
    [InlineData(-5, 0, 16)]
    public void Mark_PastTheEnds_ShouldClampTheTicks(int times, int expectedTicks, int expectedClamped)
    {
        var change = Track.Create(Plantation, Description, Rank.Dangerous, ticks: 24).Track.Mark(times);

        change.Track.Ticks.Should().Be(expectedTicks);
        change.Clamped.Should().Be(expectedClamped);
    }

    [Theory]
    [InlineData(17, 17, 0)]
    [InlineData(40, 40, 0)]
    [InlineData(-1, 0, 1)]
    [InlineData(41, 40, 1)]
    public void SetTicks_ShouldStayWithinZeroToForty(int ticks, int expectedTicks, int expectedClamped)
    {
        var change = Track.Create(Plantation, Description, Rank.Epic, ticks: 5).Track.SetTicks(ticks);

        change.Track.Ticks.Should().Be(expectedTicks);
        change.Clamped.Should().Be(expectedClamped);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    [InlineData(15, 3)]
    [InlineData(40, 10)]
    public void Boxes_ShouldCountOnlyFilledBoxes(int ticks, int expectedBoxes)
    {
        Track.Create(Plantation, Description, Rank.Epic, ticks).Track.Boxes.Should().Be(expectedBoxes);
    }

    [Fact]
    public void Edit_ShouldReplaceWhatIsGivenAndLeaveTheTicks()
    {
        var track = Track.Create(Plantation, Description, Rank.Dangerous, ticks: 16).Track;

        var reranked = track.Edit(description: null, rank: Rank.Epic);
        var redescribed = track.Edit(description: "Burn the plantation.", rank: null);

        reranked.Should().BeEquivalentTo(new { Description, Rank = Rank.Epic, Ticks = 16 });
        redescribed.Should().BeEquivalentTo(new { Description = "Burn the plantation.", Rank = Rank.Dangerous, Ticks = 16 });
    }

    [Fact]
    public void ChangingATrack_ShouldLeaveTheOriginalAsItWas()
    {
        var track = Track.Create(Plantation, Description, Rank.Dangerous, ticks: 8).Track;

        track.Mark(1);
        track.SetTicks(0);
        track.Edit(description: "Burn the plantation.", Rank.Epic);

        track.Should().BeEquivalentTo(new { Description, Rank = Rank.Dangerous, Ticks = 8 });
    }
}
