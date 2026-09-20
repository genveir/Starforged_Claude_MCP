using FluentAssertions;
using StarForged_Claude_MCP.Server.Services;

namespace StarForged_Claude_MCP.Tests.Server.Unit;

public class DiceRollerTests
{
    [Theory]
    // Beating both challenge dice is a strong hit, beating one is a weak hit, beating neither is a miss.
    [InlineData(5, 0, 1, 2, "strong hit")]
    [InlineData(5, 0, 1, 9, "weak hit")]
    [InlineData(5, 0, 9, 1, "weak hit")]
    [InlineData(2, 0, 5, 7, "miss")]
    // A challenge die is only beaten by a strictly higher action score, so ties go to the die.
    [InlineData(5, 0, 5, 5, "miss")]
    [InlineData(5, 0, 5, 2, "weak hit")]
    [InlineData(5, 0, 6, 5, "miss")]
    // The action score is uncapped, and the add can be negative.
    [InlineData(6, 8, 10, 10, "strong hit")]
    [InlineData(1, 10, 10, 9, "strong hit")]
    [InlineData(3, -4, 1, 1, "miss")]
    [InlineData(6, -5, 1, 1, "miss")]
    public void RollAction_ShouldResolveTheHitLevelAgainstBothChallengeDice(
        int actionDie, int add, int firstChallengeDie, int secondChallengeDie, string expectedResultType)
    {
        var result = CreateRoller(actionDie, firstChallengeDie, secondChallengeDie).RollAction(add);

        result.ActionDie.Should().Be(actionDie);
        result.Add.Should().Be(add);
        result.ActionScore.Should().Be(actionDie + add);
        result.ChallengeDice.Should().Equal(firstChallengeDie, secondChallengeDie);
        result.ResultType.Should().Be(expectedResultType);
    }

    [Theory]
    [InlineData(3, 3, true)]
    [InlineData(10, 10, true)]
    [InlineData(3, 4, false)]
    [InlineData(10, 1, false)]
    public void RollAction_ShouldMatchOnlyWhenBothChallengeDiceAreEqual(
        int firstChallengeDie, int secondChallengeDie, bool expectedMatch)
    {
        var result = CreateRoller(actionDie: 4, firstChallengeDie, secondChallengeDie).RollAction(add: 0);

        result.Match.Should().Be(expectedMatch);
    }

    [Theory]
    // The first challenge die is the tens digit, the second the ones, with 10 read as 0.
    [InlineData(1, 1, "11")]
    [InlineData(7, 4, "74")]
    [InlineData(3, 10, "30")]
    [InlineData(10, 3, "03")]
    [InlineData(10, 10, "00")]
    public void RollAction_ShouldReadTheChallengeDiceAsAD100(
        int firstChallengeDie, int secondChallengeDie, string expectedD100)
    {
        var result = CreateRoller(actionDie: 4, firstChallengeDie, secondChallengeDie).RollAction(add: 0);

        result.D100.Should().Be(expectedD100);
    }

    [Fact]
    public void RollAction_ShouldRollEachDieExactlyOnce()
    {
        var roller = new DiceRoller(
            actionDie: new FakeDie(4),
            firstChallengeDie: new FakeDie(2),
            secondChallengeDie: new FakeDie(8));

        roller.RollAction(add: 0);

        var rollAgain = () => roller.RollAction(add: 0);
        rollAgain.Should().Throw<InvalidOperationException>(
            because: "each die was set up for a single roll, so a second roll means a die was rolled twice");
    }

    private static DiceRoller CreateRoller(int actionDie, int firstChallengeDie, int secondChallengeDie) =>
        new(
            actionDie: new FakeDie(actionDie),
            firstChallengeDie: new FakeDie(firstChallengeDie),
            secondChallengeDie: new FakeDie(secondChallengeDie));
}
