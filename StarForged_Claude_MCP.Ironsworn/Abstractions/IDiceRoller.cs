using StarForged_Claude_MCP.Ironsworn.Dice;

namespace StarForged_Claude_MCP.Ironsworn.Abstractions;

public interface IDiceRoller
{
    ActionRollResult RollAction(int add);
}
