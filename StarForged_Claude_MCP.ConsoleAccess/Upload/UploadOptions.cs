namespace StarForged_Claude_MCP.ConsoleAccess.Upload;

public enum SinkType { None, Embedded, Document }

public enum UploadMode { None, Folder, Continuous }

public record UploadOptions(string Category, UploadMode Mode, SinkType Sink, string? FolderPath, string? SourceDocument, bool BeatLogging = false) : IConsoleAccessOptions
{
    public static UploadOptions? Parse(string[] args)
    {
        if (!CategoryArgument.TryTake(args, out var category, out var rest))
        {
            PrintUsage();
            return null;
        }

        UploadMode? mode = null;
        SinkType sink = SinkType.Embedded;
        string? folderPath = null;
        string? sourceDocument = null;
        bool beatLogging = false;

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
                case "--continuous":
                case "-c":
                    if (i + 1 >= rest.Length) { PrintUsage(); return null; }
                    mode = UploadMode.Continuous;
                    sourceDocument = rest[++i];
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

        if (sink != SinkType.Document && beatLogging)
        {
            Console.Error.WriteLine("Error: --beatLogging can only be used with --document sink.");
            PrintUsage();
            return null;
        }

        return new UploadOptions(category, mode.Value, sink, folderPath, sourceDocument, beatLogging);
    }

    public static void PrintUsage()
    {
        Program.PrintUsage();

        Console.WriteLine("Upload Options:");
        Console.WriteLine("  <category>                         Category everything in this run is stored under (required)");
        Console.WriteLine("  -f, --folder <path>                Uploads all .md files from the specified folder");
        Console.WriteLine("  -c, --continuous <sourceDocument>  Reads from stdin; flushes after 100ms of inactivity");
        Console.WriteLine("  -b, --beatLogging                  Pre-process document through BeatPreprocessor");
        Console.WriteLine("  -e, --embedded                     Write to embeddings (default)");
        Console.WriteLine("  -d, --document                     Write to documents table");
    }
}
