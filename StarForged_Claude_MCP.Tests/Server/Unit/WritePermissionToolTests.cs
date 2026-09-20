using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StarForged_Claude_MCP.Embeddings.Database.Models;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services;

namespace StarForged_Claude_MCP.Tests.Server.Unit;

public class WritePermissionToolTests
{
    private const string Category = "lore";
    private const string Filename = "derelict.md";

    public static TheoryData<string, Dictionary<string, object>> WriteToolCalls => new()
    {
        {
            "add_document",
            new Dictionary<string, object>
            {
                ["category"] = Category,
                ["filename"] = Filename,
                ["text"] = "A derelict drifts in the Forge.",
                ["indexed"] = false
            }
        },
        {
            "update_document",
            new Dictionary<string, object>
            {
                ["category"] = Category,
                ["filename"] = Filename,
                ["text"] = "A derelict drifts in the Forge."
            }
        },
        {
            "replace_document_section",
            new Dictionary<string, object>
            {
                ["category"] = Category,
                ["filename"] = Filename,
                ["section"] = "Derelicts",
                ["text"] = "## Derelicts" + "\n\nA derelict drifts in the Forge."
            }
        },
        {
            "append_to_document",
            new Dictionary<string, object>
            {
                ["category"] = Category,
                ["filename"] = Filename,
                ["text"] = "A derelict drifts in the Forge."
            }
        },
        {
            "delete_document_section",
            new Dictionary<string, object>
            {
                ["category"] = Category,
                ["filename"] = Filename,
                ["section"] = "Derelicts"
            }
        },
        {
            "index_document",
            new Dictionary<string, object>
            {
                ["category"] = Category,
                ["filename"] = Filename
            }
        },
        {
            "deindex_document",
            new Dictionary<string, object>
            {
                ["category"] = Category,
                ["filename"] = Filename
            }
        },
        {
            "archive_document",
            new Dictionary<string, object>
            {
                ["category"] = Category,
                ["filename"] = Filename
            }
        }
    };

    [Theory]
    [MemberData(nameof(WriteToolCalls))]
    public async Task WriteTool_WhenTheCategoryIsReadOnly_ShouldBeRefusedWithoutReachingTheFacade(
        string toolName, Dictionary<string, object> arguments)
    {
        var documents = CreateDocumentsMock();
        var server = CreateServer(documents, new WritePermissions());

        var response = await CallToolAsync(server, toolName, arguments);

        response.Error.Should().NotBeNull();
        response.Error!.Message.Should().Contain("read-only").And.Contain("request_write_permission");
        documents.VerifyNoOtherCalls();
    }

    [Theory]
    [MemberData(nameof(WriteToolCalls))]
    public async Task WriteTool_AfterRequestWritePermission_ShouldReachTheFacade(
        string toolName, Dictionary<string, object> arguments)
    {
        var documents = CreateDocumentsMock();
        var server = CreateServer(documents, new WritePermissions());

        var granted = await CallToolAsync(server, "request_write_permission", new Dictionary<string, object> { ["category"] = Category });
        granted.Error.Should().BeNull();

        var response = await CallToolAsync(server, toolName, arguments);

        response.Error.Should().BeNull();
        documents.Invocations.Should().NotBeEmpty(because: "'{0}' should run once writes are permitted", toolName);
    }

    [Theory]
    [MemberData(nameof(WriteToolCalls))]
    public async Task WriteTool_AfterReleaseWritePermissionRevokesThePermission_ShouldBeRefusedAgain(
        string toolName, Dictionary<string, object> arguments)
    {
        var documents = CreateDocumentsMock();
        var server = CreateServer(documents, new WritePermissions());

        await CallToolAsync(server, "request_write_permission", new Dictionary<string, object> { ["category"] = Category });
        var revoked = await CallToolAsync(server, "release_write_permission", new Dictionary<string, object> { ["category"] = Category });
        revoked.Error.Should().BeNull();

        var response = await CallToolAsync(server, toolName, arguments);

        response.Error.Should().NotBeNull();
        documents.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RequestWritePermission_ShouldPermitWritesInThatCategoryOnly()
    {
        var documents = CreateDocumentsMock();
        var server = CreateServer(documents, new WritePermissions());

        await CallToolAsync(server, "request_write_permission", new Dictionary<string, object> { ["category"] = Category });

        var response = await CallToolAsync(server, "add_document", new Dictionary<string, object>
        {
            ["category"] = "other",
            ["filename"] = Filename,
            ["text"] = "A derelict drifts in the Forge.",
            ["indexed"] = false
        });

        response.Error.Should().NotBeNull(because: "permission is granted per category, not server-wide");
        documents.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RequestWritePermission_ShouldMatchTheCategoryWithoutRegardToCase()
    {
        var documents = CreateDocumentsMock();
        var server = CreateServer(documents, new WritePermissions());

        await CallToolAsync(server, "request_write_permission", new Dictionary<string, object> { ["category"] = "Lore" });

        var response = await CallToolAsync(server, "add_document", new Dictionary<string, object>
        {
            ["category"] = "lore",
            ["filename"] = Filename,
            ["text"] = "A derelict drifts in the Forge.",
            ["indexed"] = false
        });

        response.Error.Should().BeNull();
    }

    [Fact]
    public async Task ReleaseWritePermission_WhenTheCategoryWasAlreadyReadOnly_ShouldSucceed()
    {
        var server = CreateServer(CreateDocumentsMock(), new WritePermissions());

        var response = await CallToolAsync(server, "release_write_permission", new Dictionary<string, object> { ["category"] = Category });

        response.Error.Should().BeNull();
    }

    [Fact]
    public async Task ReadTool_WhenTheCategoryIsReadOnly_ShouldStillRun()
    {
        var documents = CreateDocumentsMock();
        var server = CreateServer(documents, new WritePermissions());

        var response = await CallToolAsync(server, "document_index", new Dictionary<string, object> { ["category"] = Category });

        response.Error.Should().BeNull();
        documents.Verify(f => f.GetDocumentIndexAsync(Category), Times.Once);
    }

    private static McpServer CreateServer(Mock<IDocumentsFacade> documents, IWritePermissions writePermissions) =>
        new(new Mock<IEmbeddingsFacade>(MockBehavior.Strict).Object,
            documents.Object,
            new DiceRoller(
                actionDie: new Die(sides: 6),
                firstChallengeDie: new Die(sides: 10),
                secondChallengeDie: new Die(sides: 10)),
            writePermissions,
            NullLogger<McpServer>.Instance);

    private static Mock<IDocumentsFacade> CreateDocumentsMock()
    {
        var mock = new Mock<IDocumentsFacade>(MockBehavior.Strict);

        mock.Setup(f => f.AddDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        mock.Setup(f => f.UpdateDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(true);
        mock.Setup(f => f.ReplaceSectionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(true);
        mock.Setup(f => f.AppendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(true);
        mock.Setup(f => f.DeleteSectionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(true);
        mock.Setup(f => f.IndexDocumentAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        mock.Setup(f => f.DeindexDocumentAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        mock.Setup(f => f.DeleteDocumentAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        mock.Setup(f => f.GetDocumentIndexAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<DocumentIndexEntry>());

        return mock;
    }

    private static async Task<JsonRpcResponse> CallToolAsync(
        McpServer server, string toolName, Dictionary<string, object> arguments) =>
        await McpServerInvoker.HandleRequestAsync(server, new JsonRpcRequest
        {
            Id = toolName,
            Method = "tools/call",
            Params = new CallToolParams { Name = toolName, Arguments = arguments }
        });
}
