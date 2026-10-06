using StarForged_Claude_MCP.Ironsworn.DomainTypes;
using StarForged_Claude_MCP.Ironsworn.Errors;

namespace StarForged_Claude_MCP.ConsoleAccess.Checkpoints;

internal static class CheckpointErrors
{
    public static string Describe(ErrorCode code, CampaignName campaign, StateTrackingId name) => code switch
    {
        ErrorCode.Campaign_Not_Found => $"No campaign named '{campaign.Value}' exists.",
        ErrorCode.Checkpoint_Not_Found => $"Campaign '{campaign.Value}' has no checkpoint named '{name.Value}'.",
        _ => throw new InvalidOperationException($"Error code {code} has no console message.")
    };
}
