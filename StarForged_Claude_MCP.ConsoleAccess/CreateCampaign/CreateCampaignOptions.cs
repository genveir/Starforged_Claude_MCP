namespace StarForged_Claude_MCP.ConsoleAccess.CreateCampaign;

public record CreateCampaignOptions(string Name) : IConsoleAccessOptions
{
    public static CreateCampaignOptions? Parse(string[] args)
    {
        if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
        {
            PrintUsage();
            return null;
        }

        return new CreateCampaignOptions(args[0].Trim());
    }

    public static void PrintUsage()
    {
        Program.PrintUsage();

        Console.WriteLine("Create Campaign Options:");
        Console.WriteLine("  <name>            The name of the campaign to create");
    }
}
