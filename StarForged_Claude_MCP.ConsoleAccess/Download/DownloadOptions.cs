namespace StarForged_Claude_MCP.ConsoleAccess.Download;

public record DownloadOptions(string Category, string SourceDocument) : IConsoleAccessOptions
{
    public static DownloadOptions? Parse(string[] args)
    {
        if (args.Length != 2)
        {
            PrintUsage();
            return null;
        }

        return new DownloadOptions(args[0], args[1]);
    }

    public static void PrintUsage()
    {
        Program.PrintUsage();

        Console.WriteLine("Download Options:");
        Console.WriteLine("  <category>        The category the documents were stored under");
        Console.WriteLine("  <sourceDocument>  The source document name to download");
    }
}
