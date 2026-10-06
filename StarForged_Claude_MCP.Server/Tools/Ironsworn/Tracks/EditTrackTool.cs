using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Ironsworn.DomainTypes;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools.Ironsworn.Tracks;

public class EditTrackTool : ITool
{
    private readonly ITrackService _tracks;
    private readonly ILogger<EditTrackTool> _logger;

    public EditTrackTool(ITrackService tracks, ILogger<EditTrackTool> logger)
    {
        _tracks = tracks;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "edit_track",
        Description = "Changes the description or the rank of one of the campaign's tracks, or both. The ticks stay as they are, also when the rank changes. Returns the track as {\"track\", \"kind\", \"description\", \"rank\", \"ticks\", \"boxes\"}. tag::ironsworn::tracks",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                campaign = new { type = "string", description = ToolDescriptions.Campaign },
                track = new { type = "string", description = ToolDescriptions.Track },
                description = new { type = "string", description = "Optional. The new description, up to 200 characters; leave it out to keep the current one." },
                rank = new { type = "string", @enum = ToolArguments.Choices<Rank>(), description = "Optional. The new rank; leave it out to keep the current one." }
            },
            required = new[] { "campaign", "track" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var campaign = arguments.RequireCampaign();
        var track = arguments.RequireStateId("track");
        var description = arguments.OptionalString("description", maxLength: 200);
        var rank = arguments.OptionalChoice<Rank>("rank");

        _logger.LogDebug(
            "Executing edit_track: campaign={Campaign}, track={Track}, rank={Rank}", campaign.Value, track.Value, rank);

        if (description is null && rank is null)
            throw new ArgumentException("Description or rank is required: give the one to change, or both.");

        var edited = await _tracks.EditTrack(campaign, track, description, rank);

        return edited.Map(
            onSuccess: changed => McpJson.Serialize(TrackRecord.From(changed)),
            onFailure: code => throw StateToolErrors.ToArgumentException(code, arguments));
    }
}
