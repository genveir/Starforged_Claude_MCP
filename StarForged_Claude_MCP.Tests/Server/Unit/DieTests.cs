using FluentAssertions;
using StarForged_Claude_MCP.Server.Services;

namespace StarForged_Claude_MCP.Tests.Server.Unit;

public class DieTests
{
    [Theory]
    [InlineData(6)]
    [InlineData(10)]
    public void Roll_ShouldStayWithinTheDiesSidesAndReachBothEnds(int sides)
    {
        var die = new Die(sides);
        var seen = new HashSet<int>();

        for (var i = 0; i < 1_000; i++)
        {
            var roll = die.Roll();

            roll.Should().BeInRange(1, sides);
            seen.Add(roll);
        }

        seen.Should().Contain([1, sides], because: "both ends of the die must be reachable");
    }

    [Fact]
    public void Constructor_WithoutASingleSide_ShouldThrow()
    {
        var create = () => new Die(sides: 0);

        create.Should().Throw<ArgumentOutOfRangeException>();
    }
}
