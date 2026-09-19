namespace StarForged_Claude_MCP.ConsoleAccess.Search;

public enum SearchOutputType { None, Brief, Full }

public record SearchOptions(string Category, string SearchString, int TopK = 10) : IConsoleAccessOptions
{
    public SearchOutputType OutputType { get; set; } = SearchOutputType.Full;

    public static SearchOptions? Parse(string[] args)
    {
        if (!CategoryArgument.TryTake(args, out var category, out var rest))
        {
            PrintUsage();
            return null;
        }

        if (rest.Length == 0)
        {
            Console.Error.WriteLine("Error: <searchString> is required.");
            PrintUsage();
            return null;
        }

        string searchString = rest[0];
        int topK = 1;
        SearchOutputType outputType = SearchOutputType.Full;

        for (int i = 1; i < rest.Length; i++)
        {
            switch (rest[i])
            {
                case "--top":
                case "-t":
                    if (i + 1 >= rest.Length || !int.TryParse(rest[++i], out topK))
                    {
                        PrintUsage();
                        return null;
                    }
                    break;
                case "--brief":
                case "-b":
                    outputType = SearchOutputType.Brief;
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument: {rest[i]}");
                    PrintUsage();
                    return null;
            }
        }

        return new SearchOptions(category, searchString, topK) { OutputType = outputType };
    }

    public static void PrintUsage()
    {
        Program.PrintUsage();

        Console.WriteLine("Search Options:");
        Console.WriteLine("  <category>         The category to search (required)");
        Console.WriteLine("  <searchString>     The text to search for");
        Console.WriteLine("  -t, --top <n>      Number of results to return (default: 10)");
        Console.WriteLine("  -b, --brief        Show a compact summary instead of full text");
    }
}
