namespace StarForged_Claude_MCP.ConsoleAccess.Search;

public enum SearchOutputType { None, Brief, Full }

public record SearchOptions(string SearchString, int TopK = 10) : IConsoleAccessOptions
{
    public SearchOutputType OutputType { get; set; } = SearchOutputType.Full;

    public static SearchOptions? Parse(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return null;
        }

        string searchString = args[0];
        int topK = 1;
        SearchOutputType outputType = SearchOutputType.Full;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--top":
                case "-t":
                    if (i + 1 >= args.Length || !int.TryParse(args[++i], out topK))
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
                    Console.Error.WriteLine($"Unknown argument: {args[i]}");
                    PrintUsage();
                    return null;
            }
        }

        return new SearchOptions(searchString, topK) { OutputType = outputType };
    }

    public static void PrintUsage()
    {
        Program.PrintUsage();

        Console.WriteLine("Search Options:");
        Console.WriteLine("  <searchString>     The text to search for");
        Console.WriteLine("  -t, --top <n>      Number of results to return (default: 10)");
        Console.WriteLine("  -b, --brief        Show a compact summary instead of full text");
    }
}
