using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StarForged_Claude_MCP.Embeddings.Database.Models;
using StarForged_Claude_MCP.Embeddings.Services.Models;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services;

namespace StarForged_Claude_MCP.Tests.Server.Unit;

public class ToolDispatchTests
{
    private sealed record ToolCase(
        Dictionary<string, object> Arguments,
        Action<Mock<IEmbeddingsFacade>, Mock<IDocumentsFacade>, Mock<IWritePermissions>> VerifyDispatch);

    private const string Category = "lore";
    private const string Filename = "derelict.md";
    private const string Text = "A derelict drifts in the Forge.";
    private const string Summary = "A short summary.";

    /// <summary>
    /// One representative call per advertised tool. Every advertised tool must appear here —
    /// see <see cref="EveryAdvertisedTool_ShouldHaveADispatchCase"/>.
    /// </summary>
    private static readonly Dictionary<string, ToolCase> ToolCases = new()
    {
        ["search_index"] = new ToolCase(
            Arguments: new Dictionary<string, object>
            {
                ["query"] = "derelict in the Forge",
                ["category"] = Category
            },
            VerifyDispatch: (embeddings, documents, permissions) =>
                embeddings.Verify(f => f.SearchAsync("derelict in the Forge", Category, 3), Times.Once)),

        ["retrieve_search_results"] = new ToolCase(
            Arguments: new Dictionary<string, object> { ["ids"] = new object[] { 7, 11 } },
            VerifyDispatch: (embeddings, documents, permissions) =>
                embeddings.Verify(f => f.RetrieveByIdsAsync(It.Is<int[]>(ids => ids.SequenceEqual(new[] { 7, 11 }))), Times.Once)),

        ["add_document"] = new ToolCase(
            Arguments: new Dictionary<string, object>
            {
                ["category"] = Category,
                ["filename"] = Filename,
                ["text"] = Text,
                ["summary"] = Summary,
                ["indexed"] = true
            },
            VerifyDispatch: (embeddings, documents, permissions) =>
                documents.Verify(f => f.AddDocumentAsync(Category, Filename, Text, Summary, true), Times.Once)),

        ["update_document"] = new ToolCase(
            Arguments: new Dictionary<string, object>
            {
                ["category"] = Category,
                ["filename"] = Filename,
                ["text"] = Text,
                ["summary"] = Summary,
                ["indexed"] = false
            },
            VerifyDispatch: (embeddings, documents, permissions) =>
                documents.Verify(f => f.UpdateDocumentAsync(Category, Filename, Text, Summary, false), Times.Once)),

        ["archive_document"] = new ToolCase(
            Arguments: new Dictionary<string, object>
            {
                ["category"] = Category,
                ["filename"] = Filename
            },
            VerifyDispatch: (embeddings, documents, permissions) =>
                documents.Verify(f => f.DeleteDocumentAsync(Category, Filename), Times.Once)),

        ["get_document"] = new ToolCase(
            Arguments: new Dictionary<string, object>
            {
                ["category"] = Category,
                ["filename"] = Filename
            },
            VerifyDispatch: (embeddings, documents, permissions) =>
                documents.Verify(f => f.GetDocumentAsync(Category, Filename), Times.Once)),

        ["get_document_summary"] = new ToolCase(
            Arguments: new Dictionary<string, object>
            {
                ["category"] = Category,
                ["filename"] = Filename
            },
            VerifyDispatch: (embeddings, documents, permissions) =>
                documents.Verify(f => f.GetDocumentSummaryAsync(Category, Filename), Times.Once)),

        ["document_index"] = new ToolCase(
            Arguments: new Dictionary<string, object> { ["category"] = Category },
            VerifyDispatch: (embeddings, documents, permissions) =>
                documents.Verify(f => f.GetDocumentIndexAsync(Category), Times.Once)),

        ["get_canonical_beats"] = new ToolCase(
            Arguments: new Dictionary<string, object>
            {
                ["category"] = Category,
                ["sessionNumber"] = 5
            },
            VerifyDispatch: (embeddings, documents, permissions) =>
                documents.Verify(f => f.GetCanonicalBeatsAsync(Category, 5), Times.Once)),

        ["roll_dice"] = new ToolCase(
            Arguments: new Dictionary<string, object>(),
            VerifyDispatch: (embeddings, documents, permissions) =>
            {
                embeddings.VerifyNoOtherCalls();
                documents.VerifyNoOtherCalls();
            }),

        ["request_write_permission"] = new ToolCase(
            Arguments: new Dictionary<string, object> { ["category"] = Category },
            VerifyDispatch: (embeddings, documents, permissions) =>
                permissions.Verify(p => p.EnableWrite(Category), Times.Once)),

        ["release_write_permission"] = new ToolCase(
            Arguments: new Dictionary<string, object> { ["category"] = Category },
            VerifyDispatch: (embeddings, documents, permissions) =>
                permissions.Verify(p => p.DisableWrite(Category), Times.Once))
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
        var permissions = CreateWritePermissionsMock();
        var server = new McpServer(embeddings.Object, documents.Object, CreateDiceRoller(), permissions.Object, NullLogger<McpServer>.Instance);

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

        toolCase.VerifyDispatch(embeddings, documents, permissions);
    }

    private static List<string> ListAdvertisedToolNames()
    {
        var server = new McpServer(
            CreateEmbeddingsMock().Object,
            CreateDocumentsMock().Object,
            CreateDiceRoller(),
            CreateWritePermissionsMock().Object,
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

    private static IDiceRoller CreateDiceRoller() => new DiceRoller(
        actionDie: new Die(sides: 6),
        firstChallengeDie: new Die(sides: 10),
        secondChallengeDie: new Die(sides: 10));

    private static Mock<IWritePermissions> CreateWritePermissionsMock()
    {
        var mock = new Mock<IWritePermissions>(MockBehavior.Strict);

        mock.Setup(p => p.IsWriteEnabled(It.IsAny<string>())).Returns(true);
        mock.Setup(p => p.EnableWrite(It.IsAny<string>()));
        mock.Setup(p => p.DisableWrite(It.IsAny<string>()));

        return mock;
    }

    private static Mock<IEmbeddingsFacade> CreateEmbeddingsMock()
    {
        var mock = new Mock<IEmbeddingsFacade>(MockBehavior.Strict);

        mock.Setup(f => f.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<SearchResult>());
        mock.Setup(f => f.RetrieveByIdsAsync(It.IsAny<int[]>()))
            .ReturnsAsync(Array.Empty<TextResult>());

        return mock;
    }

    private static Mock<IDocumentsFacade> CreateDocumentsMock()
    {
        var mock = new Mock<IDocumentsFacade>(MockBehavior.Strict);

        mock.Setup(f => f.AddDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        mock.Setup(f => f.UpdateDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        mock.Setup(f => f.DeleteDocumentAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        mock.Setup(f => f.GetDocumentAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new Document());
        mock.Setup(f => f.GetDocumentSummaryAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DocumentIndexEntry());
        mock.Setup(f => f.GetDocumentIndexAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<DocumentIndexEntry>());
        mock.Setup(f => f.GetCanonicalBeatsAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<Beat>());

        return mock;
    }
}
