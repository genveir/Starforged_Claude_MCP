using StarForged_Claude_MCP.Ironsworn.DomainTypes;

namespace StarForged_Claude_MCP.ConsoleAccess.Checkpoints;

public record CreateCheckpointOptions(CampaignName Campaign, StateTrackingId Name) : IConsoleAccessOptions
{
    public static CreateCheckpointOptions? Parse(string[] args)
    {
        if (!CheckpointArguments.TryTake(args, out var campaign, out var name, out var rest))
        {
            PrintUsage();
            return null;
        }

        if (rest.Length > 0)
        {
            Console.Error.WriteLine($"Unknown argument: {rest[0]}");
            PrintUsage();
            return null;
        }

        return new CreateCheckpointOptions(campaign, name);
    }

    public static void PrintUsage()
    {
        Program.PrintUsage();

        Console.WriteLine("Create Checkpoint Options:");
        Console.WriteLine("  <campaign>        The campaign whose meters, tracks and impacts to save");
        Console.WriteLine("  <name>            The checkpoint's name, e.g. 'session-7'; an existing checkpoint of that");
        Console.WriteLine("                    name is overwritten");
    }
}
