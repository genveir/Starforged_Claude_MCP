using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools.Ironsworn.Tracks;

public class UpdateTrackTool : ITool
{
    private readonly ITrackService _tracks;
    private readonly ILogger<UpdateTrackTool> _logger;

    public UpdateTrackTool(ITrackService tracks, ILogger<UpdateTrackTool> logger)
    {
        _tracks = tracks;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "update_track",
        Description = "Changes the progress on one of the campaign's tracks, by marking it or by setting its ticks. One mark adds the rank's ticks: troublesome 12, dangerous 8, formidable 4, extreme 2, epic 1. Four ticks fill a box, and a track holds 0 to 40 ticks. Returns the track as {\"track\", \"kind\", \"description\", \"rank\", \"ticks\", \"boxes\"}; when the ticks had to be clamped to 0-40, \"clamped\" gives the amount that did not fit. tag::ironsworn::tracks",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                campaign = new { type = "string", description = ToolDescriptions.Campaign },
                track = new { type = "string", description = ToolDescriptions.Track },
                mode = new
                {
                    type = "string",
                    @enum = ToolArguments.Choices<Mode>(),
                    description = "\"mark\" to mark progress a number of times, \"set\" to replace the ticks."
                },
                value = new { type = "integer", description = "For mark, the number of times to mark progress, negative to remove progress; for set, the new number of ticks." }
            },
            required = new[] { "campaign", "track", "mode", "value" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var campaign = arguments.RequireCampaign();
        var track = arguments.RequireStateId("track");
        var mode = arguments.RequireChoice<Mode>("mode");
        var value = arguments.RequireInt("value");

        _logger.LogDebug(
            "Executing update_track: campaign={Campaign}, track={Track}, mode={Mode}, value={Value}",
            campaign.Value, track.Value, mode, value);

        var change = mode switch
        {
            Mode.Mark => await _tracks.MarkTrack(campaign, track, value),
            Mode.Set => await _tracks.SetTrackTicks(campaign, track, value),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, message: null)
        };

        return change.Map(
            onSuccess: changed => McpJson.Serialize(TrackRecord.From(changed)),
            onFailure: code => throw StateToolErrors.ToArgumentException(code, arguments));
    }

    private enum Mode { Mark, Set }
}
