using FluentAssertions;
using Moq;
using StarForged_Claude_MCP.Database.Models;
using StarForged_Claude_MCP.Ironsworn.Dice;
using StarForged_Claude_MCP.Server;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services;
using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Shared.DomainTypes;
using System.Text.Json;

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
            "replace_section_text",
            new Dictionary<string, object>
            {
                ["category"] = Category,
                ["filename"] = Filename,
                ["section"] = "Derelicts",
                ["oldText"] = "drifts",
                ["newText"] = "hangs derelict"
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

        response.ShouldHaveBeenRefused().Should().Contain("read-only").And.Contain("request_write_permission");
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
        granted.ShouldHaveSucceeded();

        var response = await CallToolAsync(server, toolName, arguments);

        response.ShouldHaveSucceeded();
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
        revoked.ShouldHaveSucceeded();

        var response = await CallToolAsync(server, toolName, arguments);

        response.ShouldHaveBeenRefused();
        documents.Verify(f => f.GetSubcategoriesAsync(new(Category)), Times.Once,
            failMessage: "request_write_permission checks that the category is a leaf");
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

        response.ShouldHaveBeenRefused().Should().Contain("read-only",
            because: "permission is granted per category, not server-wide");
        documents.Verify(f => f.GetSubcategoriesAsync(new(Category)), Times.Once,
            failMessage: "request_write_permission checks that the category is a leaf");
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

        response.ShouldHaveSucceeded();
    }

    [Fact]
    public async Task RequestWritePermission_ShouldRemindTheCallerToReleaseThatCategory()
    {
        var server = CreateServer(CreateDocumentsMock(), new WritePermissions());

        var response = await CallToolAsync(server, "request_write_permission", new Dictionary<string, object> { ["category"] = Category });

        response.ShouldHaveSucceeded();
        var text = ((CallToolResult)response.Result!).Content[0].Text;
        text.Should().Contain($"'{Category}'", because: "the model reads this text as it is, so apostrophes are not escaped as \\u0027");

        var payload = JsonSerializer.Deserialize<JsonElement>(text);
        payload.GetProperty("message").GetString().Should()
            .Contain($"Call release_write_permission for '{Category}'",
                because: "the instruction has to sit in recent context, tied to the exact category, for the caller to act on it");
    }

    [Fact]
    public async Task ReleaseWritePermission_WhenTheCategoryWasAlreadyReadOnly_ShouldSucceed()
    {
        var server = CreateServer(CreateDocumentsMock(), new WritePermissions());

        var response = await CallToolAsync(server, "release_write_permission", new Dictionary<string, object> { ["category"] = Category });

        response.ShouldHaveSucceeded();
    }

    [Fact]
    public async Task ReadTool_WhenTheCategoryIsReadOnly_ShouldStillRun()
    {
        var documents = CreateDocumentsMock();
        var server = CreateServer(documents, new WritePermissions());

        var response = await CallToolAsync(server, "list_documents", new Dictionary<string, object> { ["category"] = Category });

        response.ShouldHaveSucceeded();
        documents.Verify(f => f.GetDocumentIndexAsync(new(Category)), Times.Once);
    }

    private static McpServer CreateServer(Mock<IDocumentsFacade> documents, IWritePermissions writePermissions) =>
        McpServerFactory.Create(
            new Mock<IEmbeddingsFacade>(MockBehavior.Strict).Object,
            documents.Object,
            new DiceRoller(
                actionDie: new Die(sides: 6),
                firstChallengeDie: new Die(sides: 10),
                secondChallengeDie: new Die(sides: 10)),
            writePermissions);

    private static Mock<IDocumentsFacade> CreateDocumentsMock()
    {
        var mock = new Mock<IDocumentsFacade>(MockBehavior.Strict);

        mock.Setup(f => f.AddDocumentAsync(It.IsAny<Category>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        mock.Setup(f => f.UpdateDocumentAsync(It.IsAny<Category>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<bool?>()))
            .ReturnsAsync(true);
        mock.Setup(f => f.ReplaceSectionAsync(It.IsAny<Category>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(true);
        mock.Setup(f => f.ReplaceSectionTextAsync(It.IsAny<Category>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(1);
        mock.Setup(f => f.AppendAsync(It.IsAny<Category>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(true);
        mock.Setup(f => f.DeleteSectionAsync(It.IsAny<Category>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(true);
        mock.Setup(f => f.DeleteDocumentAsync(It.IsAny<Category>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        mock.Setup(f => f.GetDocumentIndexAsync(It.IsAny<Category>()))
            .ReturnsAsync(new List<DocumentIndexEntry>());
        mock.Setup(f => f.GetSubcategoriesAsync(It.IsAny<Category>()))
            .ReturnsAsync(new List<string>());
        mock.Setup(f => f.GetAncestorsHoldingDocumentsAsync(It.IsAny<Category>()))
            .ReturnsAsync(new List<string>());

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
