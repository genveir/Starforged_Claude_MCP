namespace StarForged_Claude_MCP.ConsoleAccess.Upload;

public enum UploadMode { None, Folder, Beats }

/// <summary>What a folder upload does about summaries, which files on disk do not carry.</summary>
public enum SummaryMode
{
    /// <summary>Ask for every file, offering the stored summary to keep.</summary>
    All,

    /// <summary>Ask only where there is no summary yet, including for new files.</summary>
    Missing,

    /// <summary>Ask for nothing and leave stored summaries alone.</summary>
    None,

    /// <summary>Ask for nothing and clear the summary of everything uploaded.</summary>
    Drop
}

public record UploadOptions(
    string Category,
    UploadMode Mode,
    string? FolderPath,
    int SessionNumber = 0,
    bool Indexed = false,
    SummaryMode Summaries = SummaryMode.Missing) : IConsoleAccessOptions
{
    public static UploadOptions? Parse(string[] args)
    {
        if (!CategoryArgument.TryTake(args, out var category, out var rest))
        {
            PrintUsage();
            return null;
        }

        UploadMode? mode = null;
        string? folderPath = null;
        int sessionNumber = 0;
        bool indexed = false;
        var summaries = SummaryMode.Missing;
        var summariesGiven = false;

        for (int i = 0; i < rest.Length; i++)
        {
            switch (rest[i])
            {
                case "--folder":
                case "-f":
                    if (i + 1 >= rest.Length) { PrintUsage(); return null; }
                    mode = UploadMode.Folder;
                    folderPath = rest[++i];
                    break;
                case "--beats":
                case "-b":
                    if (i + 1 >= rest.Length || !int.TryParse(rest[++i], out sessionNumber))
                    {
                        Console.Error.WriteLine("Error: --beats needs a session number.");
                        PrintUsage();
                        return null;
                    }
                    mode = UploadMode.Beats;
                    break;
                case "--index":
                case "-i":
                    indexed = true;
                    break;
                case "--summaries":
                case "-s":
                    if (i + 1 >= rest.Length || !Enum.TryParse(rest[++i], ignoreCase: true, out summaries))
                    {
                        Console.Error.WriteLine("Error: --summaries takes one of: all, missing, none, drop.");
                        PrintUsage();
                        return null;
                    }
                    summariesGiven = true;
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

        if (mode == UploadMode.Beats && indexed)
        {
            Console.Error.WriteLine("Error: beats are never searchable, so --index cannot be used with --beats.");
            PrintUsage();
            return null;
        }

        if (mode != UploadMode.Folder && summariesGiven)
        {
            Console.Error.WriteLine("Error: --summaries only applies to --folder; beats have no summaries.");
            PrintUsage();
            return null;
        }

        return new UploadOptions(category, mode.Value, folderPath, sessionNumber, indexed, summaries);
    }

    public static void PrintUsage()
    {
        Program.PrintUsage();

        Console.WriteLine("Upload Options:");
        Console.WriteLine("  <category>              Category everything in this run is stored under (required)");
        Console.WriteLine("  -f, --folder <path>     Stores every .md file in the folder as a document, replacing");
        Console.WriteLine("                          any already stored under the same filename");
        Console.WriteLine("  -b, --beats <session>   Reads session beats from stdin; ctrl+Z undoes the last one");
        Console.WriteLine("  -i, --index             Chunk and embed what is stored, making it searchable");
        Console.WriteLine("  -s, --summaries <mode>  How a folder upload handles summaries (default: missing)");
        Console.WriteLine("                            all      Ask for every file, blank keeps the stored one");
        Console.WriteLine("                            missing  Ask only where there is no summary yet");
        Console.WriteLine("                            none     Ask for nothing, leave stored summaries alone");
        Console.WriteLine("                            drop     Ask for nothing, clear every summary uploaded");
    }
}
