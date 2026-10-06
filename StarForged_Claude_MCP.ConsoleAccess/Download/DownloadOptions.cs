using StarForged_Claude_MCP.Shared.DomainTypes;

namespace StarForged_Claude_MCP.ConsoleAccess.Download;

public enum DownloadMode { Folder, Document }

public record DownloadOptions(
    Category Category,
    string TargetPath,
    DownloadMode Mode,
    string? Filename = null,
    bool Overwrite = false,
    bool Clean = false,
    bool DryRun = false,
    Verbosity Verbosity = Verbosity.All) : IConsoleAccessOptions
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
        bool overwrite = false;
        bool clean = false;
        bool dryRun = false;
        var verbosity = Verbosity.All;

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
                case "--overwrite":
                case "-o":
                    overwrite = true;
                    break;
                case "--clean":
                case "-c":
                    clean = true;
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

        if (clean && mode != DownloadMode.Folder)
        {
            Console.Error.WriteLine("Error: --clean only works with --folder.");
            PrintUsage();
            return null;
        }

        return new DownloadOptions(category, targetPath, mode.Value, filename, overwrite, clean, dryRun, verbosity);
    }

    public static void PrintUsage()
    {
        Program.PrintUsage();

        Console.WriteLine("Download Options:");
        Console.WriteLine("  <category>              The category to download from (required)");
        Console.WriteLine("  <path>                  Where to write to (required); see each mode below");
        Console.WriteLine("  -f, --folder            Writes every document in the category into <path> as a folder; for a");
        Console.WriteLine("                          parent category, each leaf under it gets its own nested folder");
        Console.WriteLine("  -d, --document <name>   Writes one document to the file <path>; if <path> is a folder");
        Console.WriteLine("                          (existing, or ending in a slash) it is written under its own name");
        Console.WriteLine("  -o, --overwrite         Replace existing files instead of skipping them");
        Console.WriteLine("  -c, --clean             With --folder: delete the .md files in <path> that are not part of the");
        Console.WriteLine("                          download, after listing them and asking; folders starting with a");
        Console.WriteLine("                          period are left alone");
        Console.WriteLine("  -n, --dry-run           Report what would be written, overwritten, skipped and deleted");
        Console.WriteLine("                          without touching any file; --clean lists its files without asking");
        Console.WriteLine("  -v, --verbosity <mode>  Which files a --folder download reports on (default: all)");
        Console.WriteLine("                            all      Every file, including those left unchanged");
        Console.WriteLine("                            changed  Only files that are new or overwritten, and the");
        Console.WriteLine("                                     totals of categories with changes and of the whole download");
    }
}
