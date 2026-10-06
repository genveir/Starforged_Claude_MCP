using Microsoft.Extensions.Logging;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Tools.Abstractions;

namespace StarForged_Claude_MCP.Server.Tools.Ironsworn.Meters;

public class UpdateMeterTool : ITool
{
    private readonly IMeterService _meters;
    private readonly ILogger<UpdateMeterTool> _logger;

    public UpdateMeterTool(IMeterService meters, ILogger<UpdateMeterTool> logger)
    {
        _meters = meters;
        _logger = logger;
    }

    public Tool Definition { get; } = new()
    {
        Name = "update_meter",
        Description = "Changes the value of one of the campaign's meters, by an amount or to a new value. The value is kept within the meter's range. Returns the meter as {\"name\", \"value\", \"min\", \"max\"}; when the value had to be clamped to the range, \"clamped\" gives the amount that did not fit. tag::ironsworn::meters",
        InputSchema = new
        {
            type = "object",
            properties = new
            {
                campaign = new { type = "string", description = ToolDescriptions.Campaign },
                name = new { type = "string", description = ToolDescriptions.MeterName },
                mode = new
                {
                    type = "string",
                    @enum = ToolArguments.Choices<Mode>(),
                    description = "\"delta\" to change the value by an amount, \"set\" to replace it."
                },
                value = new { type = "integer", description = "For delta, the amount to change the value by, negative to lower it; for set, the new value." }
            },
            required = new[] { "campaign", "name", "mode", "value" }
        }
    };

    public async Task<string> ExecuteAsync(ToolArguments arguments)
    {
        var campaign = arguments.RequireCampaign();
        var name = arguments.RequireStateId("name");
        var mode = arguments.RequireChoice<Mode>("mode");
        var value = arguments.RequireInt("value");

        _logger.LogDebug(
            "Executing update_meter: campaign={Campaign}, name={Name}, mode={Mode}, value={Value}",
            campaign.Value, name.Value, mode, value);

        var change = mode switch
        {
            Mode.Delta => await _meters.AdjustMeter(campaign, name, value),
            Mode.Set => await _meters.SetMeter(campaign, name, value),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, message: null)
        };

        return change.Map(
            onSuccess: changed => McpJson.Serialize(MeterRecord.From(changed)),
            onFailure: code => throw StateToolErrors.ToArgumentException(code, arguments));
    }

    private enum Mode { Delta, Set }
}
