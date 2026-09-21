using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StarForged_Claude_MCP.ConsoleAccess.Cat;
using StarForged_Claude_MCP.ConsoleAccess.Download;
using StarForged_Claude_MCP.ConsoleAccess.List;
using StarForged_Claude_MCP.ConsoleAccess.Search;
using StarForged_Claude_MCP.ConsoleAccess.Upload;
using StarForged_Claude_MCP.Embeddings;
using StarForged_Claude_MCP.Embeddings.Database;

namespace StarForged_Claude_MCP.ConsoleAccess;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.InputEncoding = System.Text.Encoding.UTF8;
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var options = ParseOptions(args);
        if (options == null) return;

        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration.AddJsonFile("appsettings.json", optional: false);

        ConfigureServices(builder.Services);

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
        var downloader = host.Services.GetRequiredService<FileDownloader>();
        var printer = host.Services.GetRequiredService<DocumentPrinter>();
        var searcher = host.Services.GetRequiredService<Searcher>();
        var lister = host.Services.GetRequiredService<CategoryLister>();
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

        switch (options)
        {
            case UploadOptions uploadOptions:
                await uploader.UploadFile(uploadOptions, lifetime.ApplicationStopping);
                break;
            case DownloadOptions downloadOptions:
                await downloader.DownloadFile(downloadOptions);
                break;
            case CatOptions catOptions:
                await printer.Print(catOptions);
                break;
            case SearchOptions searchOptions:
                await searcher.Search(searchOptions);
                break;
            case ListOptions listOptions:
                await lister.List(listOptions);
                break;
            default: throw new InvalidOperationException("Unsupported options type");
        }

        await host.StopAsync();
    }

    internal static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<FileUploader>();
        services.AddSingleton<FileDownloader>();
        services.AddSingleton<DocumentPrinter>();
        services.AddSingleton<Searcher>();
        services.AddSingleton<CategoryLister>();
        services.AddSingleton<BeatPreprocessor>();
        services.AddSingleton<ISummaryPrompt, ConsoleSummaryPrompt>();
        services.AddEmbeddingsServices();
    }

    private static IConsoleAccessOptions? ParseOptions(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return null;
        }

        return args[0].ToLower() switch
        {
            "upload" => UploadOptions.Parse(args.Skip(1).ToArray()),
            "download" => DownloadOptions.Parse(args.Skip(1).ToArray()),
            "cat" => CatOptions.Parse(args.Skip(1).ToArray()),
            "search" => SearchOptions.Parse(args.Skip(1).ToArray()),
            "list" => ListOptions.Parse(args.Skip(1).ToArray()),
            _ => HandleInvalidCommand(args[0])
        };

        IConsoleAccessOptions? HandleInvalidCommand(string command)
        {
            Console.Error.WriteLine($"Unknown command: {command}");
            PrintUsage();
            return null;
        }
    }

    internal static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  .\\ConsoleAccess.exe upload <category> --folder <path> [--index] [--summaries <mode>]");
        Console.WriteLine("  .\\ConsoleAccess.exe upload <category> --document <path> [--index] [--summaries <mode>]");
        Console.WriteLine("  .\\ConsoleAccess.exe upload <category> --beats <sessionNumber>");
        Console.WriteLine("  .\\ConsoleAccess.exe download <category> <path> --folder [--overwrite]");
        Console.WriteLine("  .\\ConsoleAccess.exe download <category> <path> --document <filename> [--overwrite]");
        Console.WriteLine("  .\\ConsoleAccess.exe download <category> <path> --beats <sessionNumber> [--overwrite]");
        Console.WriteLine("  .\\ConsoleAccess.exe cat <category> <filename>");
        Console.WriteLine("  .\\ConsoleAccess.exe search <category> <searchString> [-t <topK>]");
        Console.WriteLine("  .\\ConsoleAccess.exe list");
        Console.WriteLine("  .\\ConsoleAccess.exe list <category>");
        Console.WriteLine();
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
