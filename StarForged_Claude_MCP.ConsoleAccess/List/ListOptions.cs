namespace StarForged_Claude_MCP.ConsoleAccess.List;

public record ListOptions(string? Category) : IConsoleAccessOptions
{
    public static ListOptions? Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new ListOptions(Category: null);
        }

        if (args.Length > 1)
        {
            Console.Error.WriteLine("Error: list takes at most one argument.");
            PrintUsage();
            return null;
        }

        if (string.IsNullOrWhiteSpace(args[0]) || args[0].StartsWith('-'))
        {
            Console.Error.WriteLine($"Unknown argument: {args[0]}");
            PrintUsage();
            return null;
        }

        return new ListOptions(args[0]);
    }

    public static void PrintUsage()
    {
        Program.PrintUsage();

        Console.WriteLine("List Options:");
        Console.WriteLine("  <no arguments>               List every category that has documents");
        Console.WriteLine("  <category>                   List the document filenames in that category");
    }
}
