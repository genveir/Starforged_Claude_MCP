using Microsoft.Extensions.DependencyInjection;
using Moq;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Server;
using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Server.Tools;

namespace StarForged_Claude_MCP.Tests.Server;

/// <summary>
/// Builds a server over the given services with every tool registered the way the real server does,
/// so unit tests reach the tools through the same registration they run with.
/// </summary>
internal static class McpServerFactory
{
    public static McpServer Create(
        IEmbeddingsFacade embeddings,
        IDocumentsFacade documents,
        IDiceRoller diceRoller,
        IWritePermissions writePermissions,
        IMeterService? meters = null,
        ITrackService? tracks = null,
        IImpactService? impacts = null)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(embeddings);
        services.AddSingleton(documents);
        services.AddSingleton(diceRoller);
        services.AddSingleton(writePermissions);
        services.AddSingleton(meters ?? new Mock<IMeterService>(MockBehavior.Strict).Object);
        services.AddSingleton(tracks ?? new Mock<ITrackService>(MockBehavior.Strict).Object);
        services.AddSingleton(impacts ?? new Mock<IImpactService>(MockBehavior.Strict).Object);
        services.AddMcpTools();
        services.AddSingleton<McpServer>();

        return services.BuildServiceProvider().GetRequiredService<McpServer>();
    }
}
