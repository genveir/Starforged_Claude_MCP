namespace StarForged_Claude_MCP.ConsoleAccess.Download;

public enum DownloadMode { Folder, Document, Beats }

public record DownloadOptions(
    string Category,
    string TargetPath,
    DownloadMode Mode,
    string? Filename = null,
    int SessionNumber = 0,
    bool Overwrite = false) : IConsoleAccessOptions
{
    public static DownloadOptions? Parse(string[] args)
    {
        if (!CategoryArgument.TryTake(args, out var category, out var rest))
        {
            PrintUsage();
            return null;
        }

        if (rest.Length == 0 || string.IsNullOrWhiteSpace(rest[0]) || rest[0].StartsWith('-'))
        {
            Console.Error.WriteLine("Error: <path> is required as the second argument.");
            PrintUsage();
            return null;
        }

        var targetPath = rest[0];
        rest = rest[1..];

        DownloadMode? mode = null;
        string? filename = null;
        int sessionNumber = 0;
        bool overwrite = false;

        for (int i = 0; i < rest.Length; i++)
        {
            switch (rest[i])
            {
                case "--folder":
                case "-f":
                    mode = DownloadMode.Folder;
                    break;
                case "--document":
                case "-d":
                    if (i + 1 >= rest.Length) { PrintUsage(); return null; }
                    mode = DownloadMode.Document;
                    filename = rest[++i];
                    break;
                case "--beats":
                case "-b":
                    if (i + 1 >= rest.Length || !int.TryParse(rest[++i], out sessionNumber))
                    {
                        Console.Error.WriteLine("Error: --beats needs a session number.");
                        PrintUsage();
                        return null;
                    }
                    mode = DownloadMode.Beats;
                    break;
                case "--overwrite":
                case "-o":
                    overwrite = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument: {rest[i]}");
                    PrintUsage();
                    return null;
            }
        }

        if (mode == null)
        {
            PrintUsage();
            return null;
        }

        return new DownloadOptions(category, targetPath, mode.Value, filename, sessionNumber, overwrite);
    }

    public static void PrintUsage()
    {
        Program.PrintUsage();

        Console.WriteLine("Download Options:");
        Console.WriteLine("  <category>              The category to download from (required)");
        Console.WriteLine("  <path>                  Where to write to (required); see each mode below");
        Console.WriteLine("  -f, --folder            Writes every document in the category into <path> as a folder");
        Console.WriteLine("  -d, --document <name>   Writes one document to the file <path>; if <path> is a folder");
        Console.WriteLine("                          (existing, or ending in a slash) it is written under its own name");
        Console.WriteLine("  -b, --beats <session>   Writes the session's canonical beats to the file <path>");
        Console.WriteLine("  -o, --overwrite         Replace existing files instead of skipping them");
    }
}
