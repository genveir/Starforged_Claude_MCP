using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Ironsworn.DomainTypes;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools;

public class GetTracksTool : ITool
{
    private readonly ITrackService _tracks;
    private readonly ILogger<GetTracksTool> _logger;

    public GetTracksTool(ITrackService tracks, ILogger<GetTracksTool> logger)
    {
        _tracks = tracks;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "get_tracks",
        Description = "Reads the campaign's progress tracks: one track by id, all tracks of one kind, or all of them when track and kind are both left out. A track is returned as {\"track\", \"kind\", \"description\", \"rank\", \"ticks\", \"boxes\"}; several tracks come back as {\"tracks\": [...]}, ordered by kind and then name. tag::ironsworn",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                campaign = new { type = "string", description = ToolDescriptions.Campaign },
                track = new { type = "string", description = ToolDescriptions.Track + " Leave it out to read several tracks." },
                kind = new
                {
                    type = "string",
                    @enum = ToolArguments.Choices<TrackKind>(),
                    description = "Optional. Reads all tracks of this kind, ordered by name. Cannot be given together with track."
                }
            },
            required = new[] { "campaign" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var campaign = arguments.RequireCampaign();
        var track = arguments.OptionalStateId("track");
        var kind = arguments.OptionalChoice<TrackKind>("kind");

        _logger.LogDebug(
            "Executing get_tracks: campaign={Campaign}, track={Track}, kind={Kind}", campaign.Value, track?.Value, kind);

        if (track is not null && kind is not null)
            throw new ArgumentException(
                "Track and kind cannot be given together: give track to read one track, kind to read all tracks of " +
                "that kind, or neither to read all tracks.");

        if (track is not null)
        {
            var found = await _tracks.GetTrack(campaign, track);

            return found.Map(
                onSuccess: one => McpJson.Serialize(TrackRecord.From(one)),
                onFailure: code => throw StateToolErrors.ToArgumentException(code, arguments));
        }

        var tracks = kind is { } ofKind
            ? await _tracks.GetTracks(campaign, ofKind)
            : await _tracks.GetTracks(campaign);

        return tracks.Map(
            onSuccess: all => McpJson.Serialize(new { tracks = all.Select(TrackRecord.From) }),
            onFailure: code => throw StateToolErrors.ToArgumentException(code, arguments));
    }
}
