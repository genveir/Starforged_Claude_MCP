using StarForged_Claude_MCP.Ironsworn.DomainTypes;
using StarForged_Claude_MCP.Ironsworn.Errors;
using StarForged_Claude_MCP.Shared.Results;

namespace StarForged_Claude_MCP.Ironsworn.Abstractions;

/// <summary>
/// The progress tracks of a campaign. Every operation fails with <see cref="ErrorCode.Campaign_Not_Found"/> when the
/// campaign does not exist. Every operation on one track splits its id as <see cref="TrackId.From"/> does, failing
/// with that method's codes, and every such operation but creation fails with <see cref="ErrorCode.Track_Not_Found"/>
/// when the track does not exist.
/// </summary>
public interface ITrackService
{
    /// <summary>All of the campaign's tracks, ordered by kind and then name.</summary>
    Task<Result<IReadOnlyList<Track>, ErrorCode>> GetTracks(CampaignName campaign);

    /// <summary>The campaign's tracks of one kind, ordered by name.</summary>
    Task<Result<IReadOnlyList<Track>, ErrorCode>> GetTracks(CampaignName campaign, TrackKind kind);

    Task<Result<Track, ErrorCode>> GetTrack(CampaignName campaign, StateTrackingId track);

    /// <summary>Fails with <see cref="ErrorCode.Track_Already_Exists"/> when the campaign already has a track of that id.</summary>
    Task<Result<TrackChange, ErrorCode>> CreateTrack(
        CampaignName campaign, StateTrackingId track, string description, Rank rank, int ticks);

    Task<Result<TrackChange, ErrorCode>> MarkTrack(CampaignName campaign, StateTrackingId track, int times);

    Task<Result<TrackChange, ErrorCode>> SetTrackTicks(CampaignName campaign, StateTrackingId track, int ticks);

    /// <summary>Replaces whichever of description and rank is given, leaving the ticks as they are.</summary>
    Task<Result<Track, ErrorCode>> EditTrack(CampaignName campaign, StateTrackingId track, string? description, Rank? rank);

    /// <summary>Removes the track and returns the id it was stored under.</summary>
    Task<Result<TrackId, ErrorCode>> RemoveTrack(CampaignName campaign, StateTrackingId track);
}
