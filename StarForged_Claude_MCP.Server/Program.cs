using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StarForged_Claude_MCP.Embeddings;
using StarForged_Claude_MCP.Embeddings.Database;
using StarForged_Claude_MCP.Server.Services;

namespace StarForged_Claude_MCP.Server;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration.AddJsonFile("appsettings.json", optional: false);

        builder.Services.AddEmbeddingsServices();

        builder.Services.AddSingleton<EmbeddingsFacade>();
        builder.Services.AddSingleton<McpServer>();

        var host = builder.Build();

        // Startup validation
        try
        {
            await ValidateStartupAsync(host.Services);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Startup validation failed: {ex.Message}");
            return;
        }

        var server = host.Services.GetRequiredService<McpServer>();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        await server.RunAsync(cts.Token);
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

        // Cache is validated automatically by VectorCacheService.StartAsync during host.Build()
    }
}
