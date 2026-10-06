using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.Server.Tools.Abstractions;
using StarForged_Claude_MCP.Server.Tools.Authorization;
using StarForged_Claude_MCP.Server.Tools.CoreAccess;
using StarForged_Claude_MCP.Server.Tools.DocumentLifecycle;
using StarForged_Claude_MCP.Server.Tools.Ironsworn;
using StarForged_Claude_MCP.Server.Tools.Ironsworn.Checkpoints;
using StarForged_Claude_MCP.Server.Tools.Ironsworn.Impacts;
using StarForged_Claude_MCP.Server.Tools.Ironsworn.Meters;
using StarForged_Claude_MCP.Server.Tools.Ironsworn.Tracks;
using StarForged_Claude_MCP.Server.Tools.SectionEditing;
using StarForged_Claude_MCP.Server.Tools.SemanticSearch;

namespace StarForged_Claude_MCP.Server.Tools;

public static class ToolServicesExtension
{
    /// <summary>
    /// Registers every tool the server offers. tools/list advertises them in the order they are registered here.
    /// </summary>
    public static IServiceCollection AddMcpTools(this IServiceCollection services)
    {
        services.AddSingleton<ToolGuards>();

        services.AddSingleton<ITool, SearchIndexTool>();
        services.AddSingleton<ITool, FindTextTool>();
        services.AddSingleton<ITool, RetrieveSearchResultsTool>();
        services.AddSingleton<ITool, AddDocumentTool>();
        services.AddSingleton<ITool, UpdateDocumentTool>();
        services.AddSingleton<ITool, ReplaceDocumentSectionTool>();
        services.AddSingleton<ITool, ReplaceSectionTextTool>();
        services.AddSingleton<ITool, AppendToDocumentTool>();
        services.AddSingleton<ITool, DeleteDocumentSectionTool>();
        services.AddSingleton<ITool, ArchiveDocumentTool>();
        services.AddSingleton<ITool, GetDocumentTool>();
        services.AddSingleton<ITool, GetDocumentSummaryTool>();
        services.AddSingleton<ITool, ListDocumentsTool>();
        services.AddSingleton<ITool, RollDiceTool>();
        services.AddSingleton<ITool, GetMetersTool>();
        services.AddSingleton<ITool, UpdateMeterTool>();
        services.AddSingleton<ITool, CreateMeterTool>();
        services.AddSingleton<ITool, RemoveMeterTool>();
        services.AddSingleton<ITool, GetTracksTool>();
        services.AddSingleton<ITool, UpdateTrackTool>();
        services.AddSingleton<ITool, CreateTrackTool>();
        services.AddSingleton<ITool, EditTrackTool>();
        services.AddSingleton<ITool, RemoveTrackTool>();
        services.AddSingleton<ITool, GetImpactsTool>();
        services.AddSingleton<ITool, SetImpactTool>();
        services.AddSingleton<ITool, CreateCheckpointTool>();
        services.AddSingleton<ITool, RequestWritePermissionTool>();
        services.AddSingleton<ITool, ReleaseWritePermissionTool>();

        return services;
    }
}
