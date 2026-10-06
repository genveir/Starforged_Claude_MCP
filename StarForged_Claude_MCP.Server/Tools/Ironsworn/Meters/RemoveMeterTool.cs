using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools.Ironsworn.Meters;

public class RemoveMeterTool : ITool
{
    private readonly IMeterService _meters;
    private readonly ILogger<RemoveMeterTool> _logger;

    public RemoveMeterTool(IMeterService meters, ILogger<RemoveMeterTool> logger)
    {
        _meters = meters;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "remove_meter",
        Description = "Removes one of the campaign's meters. Returns {\"removed\": name}, with the name as the meter was stored. tag::ironsworn::meters",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                campaign = new { type = "string", description = ToolDescriptions.Campaign },
                name = new { type = "string", description = ToolDescriptions.MeterName }
            },
            required = new[] { "campaign", "name" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var campaign = arguments.RequireCampaign();
        var name = arguments.RequireStateId("name");

        _logger.LogDebug("Executing remove_meter: campaign={Campaign}, name={Name}", campaign.Value, name.Value);

        var removed = await _meters.RemoveMeter(campaign, name);

        return removed.Map(
            onSuccess: storedName => McpJson.Serialize(new { removed = storedName.Value }),
            onFailure: code => throw StateToolErrors.ToArgumentException(code, arguments));
    }
}
