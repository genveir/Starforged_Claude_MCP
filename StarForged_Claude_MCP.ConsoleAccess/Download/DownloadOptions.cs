namespace StarForged_Claude_MCP.ConsoleAccess.Download;

public record DownloadOptions(string SourceDocument) : IConsoleAccessOptions
{
    public static DownloadOptions? Parse(string[] args)
    {
        if (args.Length != 1)
        {
            PrintUsage();
            return null;
        }

        return new DownloadOptions(args[0]);
    }

    public static void PrintUsage()
    {
        Program.PrintUsage();

        Console.WriteLine("Download Options:");
        Console.WriteLine("  <sourceDocument>  The source document name to download");
    }
}
