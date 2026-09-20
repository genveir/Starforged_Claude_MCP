namespace StarForged_Claude_MCP.Server.Models;

public record ActionRollResult(
    int ActionDie,
    int Add,
    int ActionScore,
    int[] ChallengeDice,
    string ResultType,
    bool Match,
    string D100);
