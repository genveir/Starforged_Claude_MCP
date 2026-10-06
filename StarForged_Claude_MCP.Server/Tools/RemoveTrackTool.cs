using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools;

public class RemoveTrackTool : ITool
{
    private readonly ITrackService _tracks;
    private readonly ILogger<RemoveTrackTool> _logger;

    public RemoveTrackTool(ITrackService tracks, ILogger<RemoveTrackTool> logger)
    {
        _tracks = tracks;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "remove_track",
        Description = "Removes one of the campaign's tracks. Returns {\"removed\": track}, with the id as the track was stored. tag::ironsworn",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                campaign = new { type = "string", description = ToolDescriptions.Campaign },
                track = new { type = "string", description = ToolDescriptions.Track }
            },
            required = new[] { "campaign", "track" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var campaign = arguments.RequireCampaign();
        var track = arguments.RequireStateId("track");

        _logger.LogDebug("Executing remove_track: campaign={Campaign}, track={Track}", campaign.Value, track.Value);

        var removed = await _tracks.RemoveTrack(campaign, track);

        return removed.Map(
            onSuccess: storedId => McpJson.Serialize(new { removed = storedId.Value }),
            onFailure: code => throw StateToolErrors.ToArgumentException(code, arguments));
    }
}
