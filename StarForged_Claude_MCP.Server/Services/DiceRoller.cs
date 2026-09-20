using StarForged_Claude_MCP.Server.Models;

namespace StarForged_Claude_MCP.Server.Services;

public class DiceRoller : IDiceRoller
{
    private readonly IDie _actionDie;
    private readonly IDie _firstChallengeDie;
    private readonly IDie _secondChallengeDie;

    public DiceRoller(IDie actionDie, IDie firstChallengeDie, IDie secondChallengeDie)
    {
        _actionDie = actionDie;
        _firstChallengeDie = firstChallengeDie;
        _secondChallengeDie = secondChallengeDie;
    }

    public ActionRollResult RollAction(int add)
    {
        var actionDie = _actionDie.Roll();
        var challengeDice = new[] { _firstChallengeDie.Roll(), _secondChallengeDie.Roll() };

        var actionScore = actionDie + add;
        var resultType = challengeDice.Count(die => actionScore > die) switch
        {
            2 => "strong hit",
            1 => "weak hit",
            _ => "miss"
        };

        return new ActionRollResult(
            actionDie,
            add,
            actionScore,
            challengeDice,
            resultType,
            Match: challengeDice[0] == challengeDice[1],
            D100: ReadAsD100(challengeDice));
    }

    /// <summary>
    /// The challenge dice double as an oracle d100: the first is the tens digit and the second
    /// the ones, with a 10 read as a 0, so two tens read as 00 rather than 100.
    /// </summary>
    private static string ReadAsD100(int[] challengeDice) =>
        $"{challengeDice[0] % 10}{challengeDice[1] % 10}";
}
