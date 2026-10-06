using StarForged_Claude_MCP.Ironsworn.DomainTypes;
using StarForged_Claude_MCP.Ironsworn.Errors;
using StarForged_Claude_MCP.Shared.Results;

namespace StarForged_Claude_MCP.Ironsworn.Abstractions;

/// <summary>
/// The impacts marked in a campaign. Every operation fails with <see cref="ErrorCode.Campaign_Not_Found"/> when the
/// campaign does not exist, and with nothing else: marking a marked impact or clearing an unmarked one changes nothing.
/// Entities and names are matched ignoring case.
/// </summary>
public interface IImpactService
{
    /// <summary>All of the campaign's impacts, ordered by entity and then name.</summary>
    Task<Result<IReadOnlyList<Impact>, ErrorCode>> GetImpacts(CampaignName campaign);

    /// <summary>The impacts on one entity, ordered by name.</summary>
    Task<Result<IReadOnlyList<Impact>, ErrorCode>> GetImpacts(CampaignName campaign, StateTrackingId entity);

    /// <summary>Marks the impact unless it is already marked, and returns all of the campaign's impacts.</summary>
    Task<Result<IReadOnlyList<Impact>, ErrorCode>> MarkImpact(CampaignName campaign, StateTrackingId entity, string name);

    /// <summary>Clears the impact if it is marked, and returns all of the campaign's impacts.</summary>
    Task<Result<IReadOnlyList<Impact>, ErrorCode>> ClearImpact(CampaignName campaign, StateTrackingId entity, string name);
}
