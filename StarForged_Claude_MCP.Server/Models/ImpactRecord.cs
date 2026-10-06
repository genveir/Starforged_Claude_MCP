using StarForged_Claude_MCP.Ironsworn.DomainTypes;

namespace StarForged_Claude_MCP.Server.Models;

/// <summary>An impact as the impact tools return it, with its entity and name in the casing they were marked with.</summary>
public record ImpactRecord(string Entity, string Name)
{
    public static ImpactRecord From(Impact impact) => new(impact.Entity.Value, impact.Name);
}
