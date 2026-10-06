using StarForged_Claude_MCP.Database.Models;
using StarForged_Claude_MCP.Database.Repositories;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Ironsworn.DomainTypes;
using StarForged_Claude_MCP.Ironsworn.Errors;
using StarForged_Claude_MCP.Shared.Results;

namespace StarForged_Claude_MCP.Ironsworn.Services;

internal class TrackService : ITrackService
{
    private readonly CampaignResolver _campaignResolver;
    private readonly TrackRepository _trackRepository;

    public TrackService(CampaignResolver campaignResolver, TrackRepository trackRepository)
    {
        _campaignResolver = campaignResolver;
        _trackRepository = trackRepository;
    }

    public Task<Result<IReadOnlyList<Track>, ErrorCode>> GetTracks(CampaignName campaign) =>
        _campaignResolver.InCampaign(campaign, operation: async campaignId =>
        {
            var rows = await _trackRepository.GetTracks(campaignId);

            return Result<IReadOnlyList<Track>, ErrorCode>.Succeed(rows.Select(ToTrack).ToList());
        });

    public Task<Result<IReadOnlyList<Track>, ErrorCode>> GetTracks(CampaignName campaign, TrackKind kind) =>
        _campaignResolver.InCampaign(campaign, operation: async campaignId =>
        {
            var rows = await _trackRepository.GetTracks(campaignId, Stored(kind));

            return Result<IReadOnlyList<Track>, ErrorCode>.Succeed(rows.Select(ToTrack).ToList());
        });

    public Task<Result<Track, ErrorCode>> GetTrack(CampaignName campaign, StateTrackingId track) =>
        ForTrack(campaign, track, operation: async (campaignId, id) =>
        {
            var row = await _trackRepository.GetTrack(campaignId, Stored(id.Kind), id.Name);

            return row is null
                ? Result<Track, ErrorCode>.Fail(ErrorCode.Track_Not_Found)
                : Result<Track, ErrorCode>.Succeed(ToTrack(row));
        });

    public Task<Result<TrackChange, ErrorCode>> CreateTrack(
        CampaignName campaign, StateTrackingId track, string description, Rank rank, int ticks) =>
        ForTrack(campaign, track, operation: async (campaignId, id) =>
        {
            if (await _trackRepository.GetTrack(campaignId, Stored(id.Kind), id.Name) is not null)
                return Result<TrackChange, ErrorCode>.Fail(ErrorCode.Track_Already_Exists);

            var created = Track.Create(id, description, rank, ticks);
            await _trackRepository.InsertTrack(
                campaignId, Stored(id.Kind), id.Name, Stored(rank), description, created.Track.Ticks);

            return Result<TrackChange, ErrorCode>.Succeed(created);
        });

    public Task<Result<TrackChange, ErrorCode>> MarkTrack(CampaignName campaign, StateTrackingId track, int times) =>
        ChangeTicks(campaign, track, change: found => found.Mark(times));

    public Task<Result<TrackChange, ErrorCode>> SetTrackTicks(CampaignName campaign, StateTrackingId track, int ticks) =>
        ChangeTicks(campaign, track, change: found => found.SetTicks(ticks));

    public Task<Result<Track, ErrorCode>> EditTrack(
        CampaignName campaign, StateTrackingId track, string? description, Rank? rank) =>
        ForTrack(campaign, track, operation: async (campaignId, id) =>
        {
            var row = await _trackRepository.GetTrack(campaignId, Stored(id.Kind), id.Name);
            if (row is null)
                return Result<Track, ErrorCode>.Fail(ErrorCode.Track_Not_Found);

            var edited = ToTrack(row).Edit(description, rank);
            await _trackRepository.UpdateTrackDetails(
                campaignId, row.Kind, row.Name, edited.Description, Stored(edited.Rank));

            return Result<Track, ErrorCode>.Succeed(edited);
        });

    public Task<Result<TrackId, ErrorCode>> RemoveTrack(CampaignName campaign, StateTrackingId track) =>
        ForTrack(campaign, track, operation: async (campaignId, id) =>
        {
            var removed = await _trackRepository.DeleteTrack(campaignId, Stored(id.Kind), id.Name);

            return removed is null
                ? Result<TrackId, ErrorCode>.Fail(ErrorCode.Track_Not_Found)
                : Result<TrackId, ErrorCode>.Succeed(id with { Name = removed });
        });

    private Task<Result<TrackChange, ErrorCode>> ChangeTicks(
        CampaignName campaign, StateTrackingId track, Func<Track, TrackChange> change) =>
        ForTrack(campaign, track, operation: async (campaignId, id) =>
        {
            var row = await _trackRepository.GetTrack(campaignId, Stored(id.Kind), id.Name);
            if (row is null)
                return Result<TrackChange, ErrorCode>.Fail(ErrorCode.Track_Not_Found);

            var changed = change(ToTrack(row));
            await _trackRepository.UpdateTrackTicks(campaignId, row.Kind, row.Name, changed.Track.Ticks);

            return Result<TrackChange, ErrorCode>.Succeed(changed);
        });

    /// <summary>Resolves the campaign before splitting the track's id, so an unknown campaign is the failure reported.</summary>
    private Task<Result<T, ErrorCode>> ForTrack<T>(
        CampaignName campaign, StateTrackingId track, Func<int, TrackId, Task<Result<T, ErrorCode>>> operation) =>
        _campaignResolver.InCampaign(campaign, operation: campaignId => TrackId.From(track).Map(
            onSuccess: id => operation(campaignId, id),
            onFailure: Result<T, ErrorCode>.FailAsTask));

    /// <summary>Kinds and ranks are stored as their names in lowercase.</summary>
    private static string Stored(Enum value) => value.ToString().ToLowerInvariant();

    private static Track ToTrack(TrackRow row) =>
        new(new TrackId(Enum.Parse<TrackKind>(row.Kind, ignoreCase: true), row.Name),
            row.Description,
            Enum.Parse<Rank>(row.Rank, ignoreCase: true),
            row.Ticks);
}
