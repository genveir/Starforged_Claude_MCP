using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StarForged_Claude_MCP.Embeddings.Database.Models;
using StarForged_Claude_MCP.Embeddings.Services.Models;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services;

namespace StarForged_Claude_MCP.Tests.Server.Unit;

/// <summary>
/// Guards the seam between the tool names advertised by tools/list and the names
/// ExecuteToolAsync dispatches on. A tool can only be advertised if a call to it
/// under its advertised name reaches the facade method it is supposed to reach.
/// </summary>
public class ToolDispatchTests
{
    private sealed record ToolCase(
        Dictionary<string, object> Arguments,
        Action<Mock<IEmbeddingsFacade>, Mock<IDocumentsFacade>> VerifyDispatch);

    private const string Text = "A derelict drifts in the Forge.";
    private const string SourceDocument = "campaign_session_5";
    private const string Category = "session_log";

    /// <summary>
    /// One representative call per advertised tool. Every advertised tool must appear here —
    /// see <see cref="EveryAdvertisedTool_ShouldHaveADispatchCase"/>.
    /// </summary>
    private static readonly Dictionary<string, ToolCase> ToolCases = new()
    {
        ["search_index"] = new ToolCase(
            Arguments: new Dictionary<string, object> { ["query"] = "derelict in the Forge" },
            VerifyDispatch: (embeddings, documents) =>
                embeddings.Verify(f => f.SearchAsync("derelict in the Forge", 3), Times.Once)),

        ["retrieve_search_results"] = new ToolCase(
            Arguments: new Dictionary<string, object> { ["ids"] = new object[] { 7, 11 } },
            VerifyDispatch: (embeddings, documents) =>
                embeddings.Verify(f => f.RetrieveByIdsAsync(It.Is<int[]>(ids => ids.SequenceEqual(new[] { 7, 11 }))), Times.Once)),

        ["add_memory"] = new ToolCase(
            Arguments: new Dictionary<string, object> { ["text"] = Text, ["sourceDocument"] = SourceDocument },
            VerifyDispatch: (embeddings, documents) =>
                embeddings.Verify(f => f.AddMemoryAsync(Text, SourceDocument), Times.Once)),

        ["add_document"] = new ToolCase(
            Arguments: new Dictionary<string, object>
            {
                ["text"] = Text,
                ["sourceDocument"] = SourceDocument,
                ["summary"] = "A short summary.",
                ["category"] = Category
            },
            VerifyDispatch: (embeddings, documents) =>
                documents.Verify(f => f.StoreDocumentAsync(Text, SourceDocument, "A short summary.", Category), Times.Once)),

        ["get_documents"] = new ToolCase(
            Arguments: new Dictionary<string, object> { ["sourceDocument"] = SourceDocument, ["category"] = Category },
            VerifyDispatch: (embeddings, documents) =>
                documents.Verify(f => f.GetDocumentsAsync(SourceDocument, Category), Times.Once)),

        ["document_index"] = new ToolCase(
            Arguments: new Dictionary<string, object> { ["category"] = Category },
            VerifyDispatch: (embeddings, documents) =>
                documents.Verify(f => f.GetDocumentIndexAsync(Category), Times.Once)),

        ["get_canonical_beats"] = new ToolCase(
            Arguments: new Dictionary<string, object> { ["sessionNumber"] = 5, ["category"] = Category },
            VerifyDispatch: (embeddings, documents) =>
                documents.Verify(f => f.GetDocumentsAsync("SessionBeats_5", Category), Times.Once)),

        ["roll_dice"] = new ToolCase(
            Arguments: new Dictionary<string, object>(),
            VerifyDispatch: (embeddings, documents) =>
            {
                embeddings.VerifyNoOtherCalls();
                documents.VerifyNoOtherCalls();
            })
    };

    public static TheoryData<string> AdvertisedToolNames
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var name in ListAdvertisedToolNames())
                data.Add(name);
            return data;
        }
    }

    [Fact]
    public void EveryAdvertisedTool_ShouldHaveADispatchCase()
    {
        var advertised = ListAdvertisedToolNames();

        advertised.Should().BeEquivalentTo(
            ToolCases.Keys,
            because: "every tool in tools/list needs a dispatch case, and every dispatch case needs a tool in tools/list");
    }

    [Theory]
    [MemberData(nameof(AdvertisedToolNames))]
    public async Task AdvertisedTool_WhenCalledByItsAdvertisedName_ShouldReachItsFacadeMethod(string toolName)
    {
        var toolCase = ToolCases[toolName];

        var embeddings = CreateEmbeddingsMock();
        var documents = CreateDocumentsMock();
        var server = new McpServer(embeddings.Object, documents.Object, NullLogger<McpServer>.Instance);

        var response = await McpServerInvoker.HandleRequestAsync(server, new JsonRpcRequest
        {
            Id = toolName,
            Method = "tools/call",
            Params = new CallToolParams
            {
                Name = toolName,
                Arguments = toolCase.Arguments
            }
        });

        response.Error.Should().BeNull(
            because: "'{0}' is advertised by tools/list, so calling it by that name must resolve to a handler",
            toolName);
        response.Result.Should().BeOfType<CallToolResult>();

        toolCase.VerifyDispatch(embeddings, documents);
    }

    private static List<string> ListAdvertisedToolNames()
    {
        var server = new McpServer(
            CreateEmbeddingsMock().Object,
            CreateDocumentsMock().Object,
            NullLogger<McpServer>.Instance);

        var response = McpServerInvoker.HandleRequestAsync(server, new JsonRpcRequest
        {
            Id = "tools-list",
            Method = "tools/list",
            Params = new { }
        }).GetAwaiter().GetResult();

        var result = (ToolsListResult)response.Result!;
        return result.Tools.Select(t => t.Name).ToList();
    }

    private static Mock<IEmbeddingsFacade> CreateEmbeddingsMock()
    {
        var mock = new Mock<IEmbeddingsFacade>(MockBehavior.Strict);

        mock.Setup(f => f.SearchAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<SearchResult>());
        mock.Setup(f => f.AddMemoryAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Array.Empty<int>());
        mock.Setup(f => f.RetrieveByIdsAsync(It.IsAny<int[]>()))
            .ReturnsAsync(Array.Empty<TextResult>());

        return mock;
    }

    private static Mock<IDocumentsFacade> CreateDocumentsMock()
    {
        var mock = new Mock<IDocumentsFacade>(MockBehavior.Strict);

        mock.Setup(f => f.StoreDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        mock.Setup(f => f.GetDocumentsAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new List<DocumentResult>());
        mock.Setup(f => f.GetDocumentIndexAsync(It.IsAny<string?>()))
            .ReturnsAsync(new List<DocumentIndexEntry>());

        return mock;
    }
}
