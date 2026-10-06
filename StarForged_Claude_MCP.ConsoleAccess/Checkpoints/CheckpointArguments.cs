using StarForged_Claude_MCP.Ironsworn.DomainTypes;

namespace StarForged_Claude_MCP.ConsoleAccess.Checkpoints;

internal static class CheckpointArguments
{
    /// <summary>Takes the campaign and checkpoint name that every checkpoint command starts with.</summary>
    public static bool TryTake(string[] args, out CampaignName campaign, out StateTrackingId name, out string[] rest)
    {
        campaign = null!;
        name = null!;
        rest = [];

        if (args.Length < 2 || args[0].StartsWith('-') || args[1].StartsWith('-'))
        {
            Console.Error.WriteLine("Error: <campaign> and <name> are required as the first two arguments.");
            return false;
        }

        try
        {
            campaign = new CampaignName(args[0]);
            name = new StateTrackingId(args[1]);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            return false;
        }

        rest = args[2..];
        return true;
    }
}
