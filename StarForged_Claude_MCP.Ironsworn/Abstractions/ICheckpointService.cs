using StarForged_Claude_MCP.Ironsworn.DomainTypes;
using StarForged_Claude_MCP.Ironsworn.Errors;
using StarForged_Claude_MCP.Shared.Results;

namespace StarForged_Claude_MCP.Ironsworn.Abstractions;

/// <summary>
/// Named copies of all of a campaign's meters, tracks and impacts. Every operation fails with
/// <see cref="ErrorCode.Campaign_Not_Found"/> when the campaign does not exist. Names are matched ignoring case.
/// </summary>
public interface ICheckpointService
{
    /// <summary>Saves the campaign's current state under the name, overwriting any checkpoint of that name.</summary>
    Task<Result<StateTrackingId, ErrorCode>> CreateCheckpoint(CampaignName campaign, StateTrackingId name);

    /// <summary>
    /// Replaces all of the campaign's meters, tracks and impacts with the checkpoint's, which is kept. Fails with
    /// <see cref="ErrorCode.Checkpoint_Not_Found"/>, changing nothing, when the campaign has no checkpoint of that name.
    /// </summary>
    Task<Result<StateTrackingId, ErrorCode>> RestoreCheckpoint(CampaignName campaign, StateTrackingId name);
}
