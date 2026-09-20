using StarForged_Claude_MCP.Server.Models;

namespace StarForged_Claude_MCP.Server.Services;

public interface IDiceRoller
{
    ActionRollResult RollAction(int add);
}
