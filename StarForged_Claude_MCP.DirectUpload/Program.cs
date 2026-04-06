using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StarForged_Claude_MCP.Embeddings;
using StarForged_Claude_MCP.Embeddings.Database;

namespace StarForged_Claude_MCP.DirectUpload;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.InputEncoding = System.Text.Encoding.UTF8;
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var options = ParseOptions(args);
        if (options == null) return;

        if (options.Mode == UploadMode.Folder && !Directory.Exists(options.FolderPath))
        {
            Console.Error.WriteLine($"Error: Folder '{options.FolderPath}' does not exist.");
            return;
        }

        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration.AddJsonFile("appsettings.json", optional: false);

        builder.Services.AddSingleton<FileUploader>();
        builder.Services.AddSingleton<BeatPreprocessor>();
        builder.Services.AddEmbeddingsServices();

        var host = builder.Build();

        await host.StartAsync();

        // Startup validation
        try
        {
            await ValidateStartupAsync(host.Services);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Startup validation failed: {ex.Message}");
            await host.StopAsync();
            return;
        }

        var uploader = host.Services.GetRequiredService<FileUploader>();
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

        if (options.Mode == UploadMode.Folder)
            await uploader.UploadFolderAsync(options.FolderPath!, options.Sink, options.BeatLogging);
        else if (options.Mode == UploadMode.Continuous)
            await uploader.RunContinuousAsync(options.SourceDocument!, options.Sink, options.BeatLogging, lifetime.ApplicationStopping);
        else
        {
            throw new ArgumentException($"Invalid upload mode {options.Mode.ToString()}");
        }

        await host.StopAsync();
    }

    private static UploadOptions? ParseOptions(string[] args)
    {
        UploadMode? mode = null;
        SinkType sink = SinkType.Embedded;
        string? folderPath = null;
        string? sourceDocument = null;
        bool beatLogging = false;

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

        if (beatLogging && sink != SinkType.Document)
        {
            Console.Error.WriteLine("Error: --beatLogging can only be used with --document sink.");
            PrintUsage();
        }

        return new UploadOptions(mode.Value, sink, folderPath, sourceDocument, beatLogging);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  StarForged_Claude_MCP.DirectUpload --folder <path> [--embedded | --document]");
        Console.WriteLine("  StarForged_Claude_MCP.DirectUpload --continuous <sourceDocument> [--embedded | --document]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -f, --folder <path>                Uploads all .md files from the specified folder");
        Console.WriteLine("  -c, --continuous <sourceDocument>  Reads from stdin; flushes after 100ms of inactivity");
        Console.WriteLine("  -b, --beatLogging                  Pre-process document through BeatPreprocessor");
        Console.WriteLine("  -e, --embedded                     Write to embeddings (default)");
        Console.WriteLine("  -d, --document                     Write to documents table");
    }

    private static async Task ValidateStartupAsync(IServiceProvider services)
    {
        // Validate ONNX model exists
        var modelPath = Path.Combine(AppContext.BaseDirectory, "model.onnx");
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"ONNX model not found at: {modelPath}");
        }

        // Validate database connectivity
        var db = services.GetRequiredService<DbInterface>();
        await db.TestConnection();

        Console.WriteLine("✓ All dependencies validated");
    }
}
