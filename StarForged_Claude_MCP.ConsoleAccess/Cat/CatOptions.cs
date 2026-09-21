namespace StarForged_Claude_MCP.ConsoleAccess.Cat;

public record CatOptions(string Category, string Filename) : IConsoleAccessOptions
{
    public static CatOptions? Parse(string[] args)
    {
        if (args.Length != 2)
        {
            PrintUsage();
            return null;
        }

        return new CatOptions(args[0], args[1]);
    }

    public static void PrintUsage()
    {
        Program.PrintUsage();

        Console.WriteLine("Cat Options:");
        Console.WriteLine("  <category>        The category the document is stored under");
        Console.WriteLine("  <filename>        The filename of the document to print");
    }
}
