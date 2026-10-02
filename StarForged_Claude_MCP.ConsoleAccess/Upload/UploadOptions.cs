namespace StarForged_Claude_MCP.ConsoleAccess.Upload;

public enum UploadMode { None, Folder, Document, Beats }

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

public enum IndexMode
{
    /// <summary>Ask for nothing and index everything uploaded.</summary>
    All,

    /// <summary>Ask for every file, offering to keep whether a stored file is indexed.</summary>
    Ask,

    /// <summary>Ask only for new files and leave stored files indexed or not as they are.</summary>
    New,

    /// <summary>Ask for nothing and remove everything uploaded from the index.</summary>
    Drop
}

public record UploadOptions(
    string Category,
    UploadMode Mode,
    string? SourcePath,
    int SessionNumber = 0,
    IndexMode Index = IndexMode.New,
    SummaryMode Summaries = SummaryMode.Missing,
    bool DryRun = false,
    Verbosity Verbosity = Verbosity.All) : IConsoleAccessOptions
{
    public static UploadOptions? Parse(string[] args)
    {
        if (!CategoryArgument.TryTake(args, out var category, out var rest))
        {
            PrintUsage();
            return null;
        }

        UploadMode? mode = null;
        string? sourcePath = null;
        int sessionNumber = 0;
        var index = IndexMode.New;
        var indexGiven = false;
        var summaries = SummaryMode.Missing;
        var summariesGiven = false;
        var dryRun = false;
        var verbosity = Verbosity.All;
        var verbosityGiven = false;

        for (int i = 0; i < rest.Length; i++)
        {
            switch (rest[i])
            {
                case "--folder":
                case "-f":
                    if (i + 1 >= rest.Length) { PrintUsage(); return null; }
                    mode = UploadMode.Folder;
                    sourcePath = rest[++i];
                    break;
                case "--document":
                case "-d":
                    if (i + 1 >= rest.Length) { PrintUsage(); return null; }
                    mode = UploadMode.Document;
                    sourcePath = rest[++i];
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
                    if (i + 1 >= rest.Length || !Enum.TryParse(rest[++i], ignoreCase: true, out index))
                    {
                        Console.Error.WriteLine("Error: --index takes one of: all, ask, new, drop.");
                        PrintUsage();
                        return null;
                    }
                    indexGiven = true;
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
                case "--dry-run":
                case "-n":
                    dryRun = true;
                    break;
                case "--verbosity":
                case "-v":
                    if (i + 1 >= rest.Length || !Enum.TryParse(rest[++i], ignoreCase: true, out verbosity))
                    {
                        Console.Error.WriteLine("Error: --verbosity takes one of: all, changed.");
                        PrintUsage();
                        return null;
                    }
                    verbosityGiven = true;
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

        if (mode == UploadMode.Beats && indexGiven)
        {
            Console.Error.WriteLine("Error: beats are never searchable, so --index cannot be used with --beats.");
            PrintUsage();
            return null;
        }

        if (mode == UploadMode.Beats && summariesGiven)
        {
            Console.Error.WriteLine("Error: beats have no summaries, so --summaries cannot be used with --beats.");
            PrintUsage();
            return null;
        }

        if (mode == UploadMode.Beats && dryRun)
        {
            Console.Error.WriteLine("Error: beats are stored as they are typed, so --dry-run cannot be used with --beats.");
            PrintUsage();
            return null;
        }

        if (mode == UploadMode.Beats && verbosityGiven)
        {
            Console.Error.WriteLine("Error: beats report every one stored, so --verbosity cannot be used with --beats.");
            PrintUsage();
            return null;
        }

        return new UploadOptions(category, mode.Value, sourcePath, sessionNumber, index, summaries, dryRun, verbosity);
    }

    public static void PrintUsage()
    {
        Program.PrintUsage();

        Console.WriteLine("Upload Options:");
        Console.WriteLine("  <category>              Category everything in this run is stored under (required)");
        Console.WriteLine("  -f, --folder <path>     Stores every .md file in the folder as a document, replacing");
        Console.WriteLine("                          any already stored under the same filename; each subfolder");
        Console.WriteLine("                          becomes a subcategory, and folders starting with '.' are skipped");
        Console.WriteLine("  -d, --document <path>   Stores a single file as a document, replacing any already");
        Console.WriteLine("                          stored under the same filename");
        Console.WriteLine("  -b, --beats <session>   Reads session beats from stdin; ctrl+Z undoes the last one");
        Console.WriteLine("  -i, --index <mode>      How a folder or document upload handles indexing, which");
        Console.WriteLine("                          chunks and embeds a document to make it searchable");
        Console.WriteLine("                          (default: new)");
        Console.WriteLine("                            all      Ask for nothing, index every file uploaded");
        Console.WriteLine("                            ask      Ask for every file, blank keeps what is stored");
        Console.WriteLine("                            new      Ask only for new files, leave stored ones as they are");
        Console.WriteLine("                            drop     Ask for nothing, remove every file uploaded from the index");
        Console.WriteLine("  -s, --summaries <mode>  How a folder or document upload handles summaries");
        Console.WriteLine("                          (default: missing)");
        Console.WriteLine("                            all      Ask for every file, blank keeps the stored one");
        Console.WriteLine("                            missing  Ask only where there is no summary yet");
        Console.WriteLine("                            none     Ask for nothing, leave stored summaries alone");
        Console.WriteLine("                            drop     Ask for nothing, clear every summary uploaded");
        Console.WriteLine("  -n, --dry-run           Report what a folder or document upload would store, replace and");
        Console.WriteLine("                          index without writing anything; nothing is asked, and every");
        Console.WriteLine("                          question is taken as answered blank");
        Console.WriteLine("  -v, --verbosity <mode>  Which files a folder or document upload reports on (default: all)");
        Console.WriteLine("                            all      Every file, including those left unchanged");
        Console.WriteLine("                            changed  Only files that are stored or replaced, and the");
        Console.WriteLine("                                     totals of categories with changes and of the whole upload");
    }
}
