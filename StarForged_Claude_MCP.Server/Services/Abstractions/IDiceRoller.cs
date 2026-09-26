using StarForged_Claude_MCP.Server.Models;

namespace StarForged_Claude_MCP.Server.Services.Abstractions;

public interface IDiceRoller
{
    ActionRollResult RollAction(int add);
}
