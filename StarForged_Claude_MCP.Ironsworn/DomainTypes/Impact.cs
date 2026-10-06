namespace StarForged_Claude_MCP.Ironsworn.DomainTypes;

/// <summary>
/// An impact marked on something: the character, a vehicle or a module. Only which impacts are marked on what
/// is kept; what an impact means is left to the game.
/// </summary>
public record Impact(StateTrackingId Entity, string Name);
