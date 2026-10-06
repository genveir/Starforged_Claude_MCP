using StarForged_Claude_MCP.Ironsworn.Errors;

namespace StarForged_Claude_MCP.Server.Tools;

/// <summary>
/// Where every state tool's failure codes get their model-facing wording. A message names its subject by
/// reading it from the tool's own arguments, so the model sees the value it sent.
/// </summary>
public static class StateToolErrors
{
    public static ArgumentException ToArgumentException(ErrorCode code, ToolArguments arguments) => code switch
    {
        ErrorCode.Campaign_Not_Found => new ArgumentException(
            $"No campaign named '{arguments.RequireCampaign().Value}' exists. Campaigns are set up by the player " +
            "outside this server; check the campaign name you were given, and ask the player if it is right."),
        _ => throw new InvalidOperationException($"Error code {code} has no model-facing message.")
    };
}
