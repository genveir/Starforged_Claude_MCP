using StarForged_Claude_MCP.Shared.DomainTypes;

namespace StarForged_Claude_MCP.ConsoleAccess.Upload;

public enum UploadMode { Folder, Document }

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
    Category Category,
    UploadMode Mode,
    string SourcePath,
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
        var index = IndexMode.New;
        var summaries = SummaryMode.Missing;
        var dryRun = false;
        var verbosity = Verbosity.All;

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
                case "--index":
                case "-i":
                    if (i + 1 >= rest.Length || !Enum.TryParse(rest[++i], ignoreCase: true, out index))
                    {
                        Console.Error.WriteLine("Error: --index takes one of: all, ask, new, drop.");
                        PrintUsage();
                        return null;
                    }
                    break;
                case "--summaries":
                case "-s":
                    if (i + 1 >= rest.Length || !Enum.TryParse(rest[++i], ignoreCase: true, out summaries))
                    {
                        Console.Error.WriteLine("Error: --summaries takes one of: all, missing, none, drop.");
                        PrintUsage();
                        return null;
                    }
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

        return new UploadOptions(category, mode.Value, sourcePath!, index, summaries, dryRun, verbosity);
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
        Console.WriteLine("  -i, --index <mode>      How an upload handles indexing, which chunks and embeds a");
        Console.WriteLine("                          document to make it searchable");
        Console.WriteLine("                          (default: new)");
        Console.WriteLine("                            all      Ask for nothing, index every file uploaded");
        Console.WriteLine("                            ask      Ask for every file, blank keeps what is stored");
        Console.WriteLine("                            new      Ask only for new files, leave stored ones as they are");
        Console.WriteLine("                            drop     Ask for nothing, remove every file uploaded from the index");
        Console.WriteLine("  -s, --summaries <mode>  How an upload handles summaries");
        Console.WriteLine("                          (default: missing)");
        Console.WriteLine("                            all      Ask for every file, blank keeps the stored one");
        Console.WriteLine("                            missing  Ask only where there is no summary yet");
        Console.WriteLine("                            none     Ask for nothing, leave stored summaries alone");
        Console.WriteLine("                            drop     Ask for nothing, clear every summary uploaded");
        Console.WriteLine("  -n, --dry-run           Report what an upload would store, replace and index without");
        Console.WriteLine("                          writing anything; nothing is asked, and every question is");
        Console.WriteLine("                          taken as answered blank");
        Console.WriteLine("  -v, --verbosity <mode>  Which files an upload reports on (default: all)");
        Console.WriteLine("                            all      Every file, including those left unchanged");
        Console.WriteLine("                            changed  Only files that are stored or replaced, and the");
        Console.WriteLine("                                     totals of categories with changes and of the whole upload");
    }
}
