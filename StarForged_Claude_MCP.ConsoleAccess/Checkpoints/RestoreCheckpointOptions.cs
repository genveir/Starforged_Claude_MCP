using StarForged_Claude_MCP.Ironsworn.DomainTypes;

namespace StarForged_Claude_MCP.ConsoleAccess.Checkpoints;

public record RestoreCheckpointOptions(CampaignName Campaign, StateTrackingId Name, bool Yes = false) : IConsoleAccessOptions
{
    public static RestoreCheckpointOptions? Parse(string[] args)
    {
        if (!CheckpointArguments.TryTake(args, out var campaign, out var name, out var rest))
        {
            PrintUsage();
            return null;
        }

        bool yes = false;

        foreach (var arg in rest)
        {
            switch (arg)
            {
                case "--yes":
                case "-y":
                    yes = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument: {arg}");
                    PrintUsage();
                    return null;
            }
        }

        return new RestoreCheckpointOptions(campaign, name, yes);
    }

    public static void PrintUsage()
    {
        Program.PrintUsage();

        Console.WriteLine("Restore Checkpoint Options:");
        Console.WriteLine("  <campaign>        The campaign to restore");
        Console.WriteLine("  <name>            The checkpoint to restore; it replaces all of the campaign's meters,");
        Console.WriteLine("                    tracks and impacts, and is kept so it can be restored again");
        Console.WriteLine("  -y, --yes         Restore without asking first");
    }
}
