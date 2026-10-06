using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Ironsworn.DomainTypes;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools;

public class CreateTrackTool : ITool
{
    private readonly ITrackService _tracks;
    private readonly ILogger<CreateTrackTool> _logger;

    public CreateTrackTool(ITrackService tracks, ILogger<CreateTrackTool> logger)
    {
        _tracks = tracks;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "create_track",
        Description = "Creates a progress track in the campaign, with a description and a rank. Progress starts at 0 ticks unless ticks is given. Returns the track as {\"track\", \"kind\", \"description\", \"rank\", \"ticks\", \"boxes\"}; when the starting ticks had to be clamped to 0-40, \"clamped\" gives the amount that did not fit. tag::ironsworn",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                campaign = new { type = "string", description = ToolDescriptions.Campaign },
                track = new { type = "string", description = ToolDescriptions.Track },
                description = new { type = "string", description = "What the track is for, e.g. the vow as sworn: 'Handle the drug plantation.' Up to 200 characters." },
                rank = new { type = "string", @enum = ToolArguments.Choices<Rank>(), description = "The track's rank." },
                ticks = new { type = "integer", description = "Optional. The starting progress in ticks, clamped to 0-40; defaults to 0." }
            },
            required = new[] { "campaign", "track", "description", "rank" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var campaign = arguments.RequireCampaign();
        var track = arguments.RequireStateId("track");
        var description = arguments.RequireString("description", maxLength: 200);
        var rank = arguments.RequireChoice<Rank>("rank");
        var ticks = arguments.OptionalInt("ticks", defaultValue: 0);

        _logger.LogDebug(
            "Executing create_track: campaign={Campaign}, track={Track}, rank={Rank}, ticks={Ticks}",
            campaign.Value, track.Value, rank, ticks);

        var created = await _tracks.CreateTrack(campaign, track, description, rank, ticks);

        return created.Map(
            onSuccess: change => McpJson.Serialize(TrackRecord.From(change)),
            onFailure: code => throw StateToolErrors.ToArgumentException(code, arguments));
    }
}
