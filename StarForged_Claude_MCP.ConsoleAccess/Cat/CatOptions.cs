using StarForged_Claude_MCP.Shared.DomainTypes;

namespace StarForged_Claude_MCP.ConsoleAccess.Cat;

public record CatOptions(Category Category, string Filename) : IConsoleAccessOptions
{
    public static CatOptions? Parse(string[] args)
    {
        if (args.Length != 2 || !CategoryArgument.TryTake(args, out var category, out var rest))
        {
            PrintUsage();
            return null;
        }

        return new CatOptions(category, rest[0]);
    }

    public static void PrintUsage()
    {
        Program.PrintUsage();

        Console.WriteLine("Cat Options:");
        Console.WriteLine("  <category>        The category the document is stored under");
        Console.WriteLine("  <filename>        The filename of the document to print");
    }
}
