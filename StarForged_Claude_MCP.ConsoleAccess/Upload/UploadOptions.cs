namespace StarForged_Claude_MCP.ConsoleAccess.Upload;

public enum SinkType { None, Embedded, Document }

public enum UploadMode { None, Folder, Continuous }

public record UploadOptions(UploadMode Mode, SinkType Sink, string? FolderPath, string? SourceDocument, bool BeatLogging = false, string? Category = null) : IConsoleAccessOptions
{
    public static UploadOptions? Parse(string[] args)
    {
        UploadMode? mode = null;
        SinkType sink = SinkType.Embedded;
        string? folderPath = null;
        string? sourceDocument = null;
        bool beatLogging = false;
        string? category = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--folder":
                case "-f":
                    if (i + 1 >= args.Length) { PrintUsage(); return null; }
                    mode = UploadMode.Folder;
                    folderPath = args[++i];
                    break;
                case "--continuous":
                case "-c":
                    if (i + 1 >= args.Length) { PrintUsage(); return null; }
                    mode = UploadMode.Continuous;
                    sourceDocument = args[++i];
                    break;
                case "--embedded":
                case "-e":
                    sink = SinkType.Embedded;
                    break;
                case "--document":
                case "-d":
                    sink = SinkType.Document;
                    break;
                case "--beatLogging":
                case "-b":
                    beatLogging = true;
                    break;
                case "--category":
                case "-cat":
                    if (i + 1 >= args.Length) { PrintUsage(); return null; }
                    category = args[++i];
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument: {args[i]}");
                    PrintUsage();
                    return null;
            }
        }

        if (mode == null)
        {
            PrintUsage();
            return null;
        }

        if (sink != SinkType.Document)
        {
            if (!ValidateNonDocumentOptions(beatLogging, category))
            {
                PrintUsage();
                return null;
            }
        }

        return new UploadOptions(mode.Value, sink, folderPath, sourceDocument, beatLogging, category);
    }

    private static bool ValidateNonDocumentOptions(bool beatLogging, string? category)
    {
        if (beatLogging)
        {
            Console.Error.WriteLine("Error: --beatLogging can only be used with --document sink.");
            return false;
        }
        if (category != null)
        {
            Console.Error.WriteLine("Error: --category can only be used with --document sink.");
            return false;
        }
        return true;
    }

    public static void PrintUsage()
    {
        Program.PrintUsage();

        Console.WriteLine("Upload Options:");
        Console.WriteLine("  -f, --folder <path>                Uploads all .md files from the specified folder");
        Console.WriteLine("  -c, --continuous <sourceDocument>  Reads from stdin; flushes after 100ms of inactivity");
        Console.WriteLine("  -b, --beatLogging                  Pre-process document through BeatPreprocessor");
        Console.WriteLine("  -e, --embedded                     Write to embeddings (default)");
        Console.WriteLine("  -d, --document                     Write to documents table");
        Console.WriteLine("  -cat, --category <category>        Category label to attach to stored documents (document sink only)");
    }
}
