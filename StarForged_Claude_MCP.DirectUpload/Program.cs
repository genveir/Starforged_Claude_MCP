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
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: StarForged_Claude_MCP.DirectUpload <folder_path>");
            Console.WriteLine("Uploads all files from the specified folder to the embeddings database.");
            return;
        }

        var folderPath = args[0];

        if (!Directory.Exists(folderPath))
        {
            Console.WriteLine($"Error: Folder '{folderPath}' does not exist.");
            return;
        }

        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration.AddJsonFile("appsettings.json", optional: false);

        builder.Services.AddSingleton<FileUploader>();
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
        await uploader.UploadFolderAsync(folderPath);

        await host.StopAsync();
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
