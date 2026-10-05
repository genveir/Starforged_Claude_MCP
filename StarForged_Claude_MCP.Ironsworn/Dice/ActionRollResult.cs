namespace StarForged_Claude_MCP.Ironsworn.Dice;

public record ActionRollResult(
    int ActionDie,
    int Add,
    int ActionScore,
    int[] ChallengeDice,
    string ResultType,
    bool Match,
    string D100);
