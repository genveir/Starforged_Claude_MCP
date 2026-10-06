using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools.Ironsworn.Checkpoints;

public class CreateCheckpointTool : ITool
{
    private readonly ICheckpointService _checkpoints;
    private readonly ILogger<CreateCheckpointTool> _logger;

    public CreateCheckpointTool(ICheckpointService checkpoints, ILogger<CreateCheckpointTool> logger)
    {
        _checkpoints = checkpoints;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "create_checkpoint",
        Description = "Saves a copy of all of the campaign's meters, tracks and impacts under a name. A checkpoint with the same name is overwritten. Returns {\"checkpoint\": name}. tag::ironsworn::checkpoints",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                campaign = new { type = "string", description = ToolDescriptions.Campaign },
                name = new { type = "string", description = "The checkpoint's name, e.g. 'session-7': letters, digits and hyphens." }
            },
            required = new[] { "campaign", "name" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var campaign = arguments.RequireCampaign();
        var name = arguments.RequireStateId("name");

        _logger.LogDebug("Executing create_checkpoint: campaign={Campaign}, name={Name}", campaign.Value, name.Value);

        var created = await _checkpoints.CreateCheckpoint(campaign, name);

        return created.Map(
            onSuccess: checkpoint => McpJson.Serialize(new { checkpoint = checkpoint.Value }),
            onFailure: code => throw StateToolErrors.ToArgumentException(code, arguments));
    }
}
