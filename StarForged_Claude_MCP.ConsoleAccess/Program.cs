using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StarForged_Claude_MCP.ConsoleAccess.Download;
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

        builder.Services.AddSingleton<FileUploader>();
        builder.Services.AddSingleton<FileDownloader>();
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
        var downloader = host.Services.GetRequiredService<FileDownloader>();
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

        switch (options)
        {
            case UploadOptions uploadOptions:
                await uploader.UploadFile(uploadOptions, lifetime.ApplicationStopping);
                break;
            case DownloadOptions downloadOptions:
                await downloader.DownloadFile(downloadOptions);
                break;
            default: throw new InvalidOperationException("Unsupported options type");
        }

        await host.StopAsync();
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
        Console.WriteLine("  StarForged_Claude_MCP.DirectUpload upload --folder <path> [--embedded | --document]");
        Console.WriteLine("  StarForged_Claude_MCP.DirectUpload upload --continuous <sourceDocument> [--embedded | --document] [--beatLogging]");
        Console.WriteLine("  StarForged_Claude_MCP.DirectUpload download <sourceDocument>");
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
