using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools;

public class RollDiceTool : ITool
{
    private readonly IDiceRoller _diceRoller;
    private readonly ILogger<RollDiceTool> _logger;

    public RollDiceTool(IDiceRoller diceRoller, ILogger<RollDiceTool> logger)
    {
        _diceRoller = diceRoller;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "roll_dice",
        Description = "Rolls the dice for an Ironsworn action roll: one d6 (action die) and two d10s (challenge dice). Resolves the roll and returns actionScore (action die plus add, uncapped), resultType (\"strong hit\", \"weak hit\" or \"miss\"), match (whether the two challenge dice are equal), and d100 (the challenge dice read as an oracle roll, the first as the tens digit). tag::ironsworn",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                purpose = new { type = "string", description = "State, before rolling, what this roll decides and how you will read the result." },
                add = new { type = "number", description = "Total to add to the action die: the relevant stat plus any other adds. Defaults to 0." }
            },
            required = new[] { "purpose" }
        }
    };

    public Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var add = arguments.OptionalInt("add", defaultValue: 0);
        var roll = _diceRoller.RollAction(add);

        _logger.LogDebug(
            "Executing roll_dice: actionDie={ActionDie}, add={Add}, actionScore={ActionScore}, challengeDice={ChallengeDice}, resultType={ResultType}, match={Match}, d100={D100}",
            roll.ActionDie, roll.Add, roll.ActionScore, string.Join(",", roll.ChallengeDice), roll.ResultType, roll.Match, roll.D100);

        return Task.FromResult(McpJson.Serialize(roll));
    }
}
