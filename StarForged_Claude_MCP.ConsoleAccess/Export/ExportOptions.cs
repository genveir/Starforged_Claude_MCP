namespace StarForged_Claude_MCP.ConsoleAccess.Export;

public record ExportOptions(string Category, string OutputFolder, bool Overwrite = false) : IConsoleAccessOptions
{
    public static ExportOptions? Parse(string[] args)
    {
        string? category = null;
        string? outputFolder = null;
        bool overwrite = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--category":
                case "-cat":
                    if (i + 1 >= args.Length) { PrintUsage(); return null; }
                    category = args[++i];
                    break;
                case "--output":
                case "-o":
                    if (i + 1 >= args.Length) { PrintUsage(); return null; }
                    outputFolder = args[++i];
                    break;
                case "--overwrite":
                    overwrite = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument: {args[i]}");
                    PrintUsage();
                    return null;
            }
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            Console.Error.WriteLine("Error: --category is required.");
            PrintUsage();
            return null;
        }

        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            Console.Error.WriteLine("Error: --output is required.");
            PrintUsage();
            return null;
        }

        return new ExportOptions(category, outputFolder, overwrite);
    }

    public static void PrintUsage()
    {
        Program.PrintUsage();

        Console.WriteLine("Export Options:");
        Console.WriteLine("  -cat, --category <category>  The category to export (required)");
        Console.WriteLine("  -o, --output <path>          Folder to write the .md files to (created if missing)");
        Console.WriteLine("      --overwrite              Replace existing files instead of skipping them");
    }
}
