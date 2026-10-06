using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StarForged_Claude_MCP.Database.Models;
using StarForged_Claude_MCP.Embeddings.Services.Models;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Ironsworn.Dice;
using StarForged_Claude_MCP.Ironsworn.DomainTypes;
using StarForged_Claude_MCP.Ironsworn.Errors;
using StarForged_Claude_MCP.Server;
using StarForged_Claude_MCP.Server.Models;
using StarForged_Claude_MCP.Server.Services.Abstractions;
using StarForged_Claude_MCP.Server.Tools.Abstractions;
using StarForged_Claude_MCP.Shared.DomainTypes;
using StarForged_Claude_MCP.Shared.Results;

namespace StarForged_Claude_MCP.Tests.Server.Unit;

public class ToolDispatchTests
{
    private sealed record ToolCase(
        Dictionary<string, object> Arguments,
        Action<Mock<IEmbeddingsFacade>, Mock<IDocumentsFacade>, Mock<IWritePermissions>, Mock<IMeterService>, Mock<ITrackService>> VerifyDispatch);

    private const string Category = "lore";
    private const string Filename = "derelict.md";
    private const string Text = "A derelict drifts in the Forge.";
    private const string Summary = "A short summary.";
    private const string Section = "Derelicts";
    private const string Campaign = "Iron Expanse";
    private const string MeterName = "health";
    private const string TrackArgument = "vow.handle-the-plantation";
    private const string TrackDescription = "Handle the drug plantation.";

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
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
                embeddings.Verify(f => f.SearchAsync("derelict in the Forge", new(Category), 3), Times.Once)),

        ["find_text"] = new ToolCase(
            Arguments: new Dictionary<string, object>
            {
                ["category"] = Category,
                ["text"] = "Bluejay",
                ["wholeWord"] = true,
                ["filename"] = Filename
            },
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
                documents.Verify(f => f.FindTextAsync(new(Category), "Bluejay", true, Filename), Times.Once)),

        ["retrieve_search_results"] = new ToolCase(
            Arguments: new Dictionary<string, object> { ["ids"] = new object[] { 7, 11 } },
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
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
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
                documents.Verify(f => f.AddDocumentAsync(new(Category), Filename, Text, Summary, true), Times.Once)),

        ["update_document"] = new ToolCase(
            Arguments: new Dictionary<string, object>
            {
                ["category"] = Category,
                ["filename"] = Filename,
                ["text"] = Text,
                ["summary"] = Summary,
                ["indexed"] = false
            },
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
                documents.Verify(f => f.UpdateDocumentAsync(new(Category), Filename, Text, Summary, false), Times.Once)),

        ["replace_document_section"] = new ToolCase(
            Arguments: new Dictionary<string, object>
            {
                ["category"] = Category,
                ["filename"] = Filename,
                ["section"] = Section,
                ["text"] = Text,
                ["summary"] = Summary
            },
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
                documents.Verify(f => f.ReplaceSectionAsync(new(Category), Filename, Section, Text, Summary), Times.Once)),

        ["replace_section_text"] = new ToolCase(
            Arguments: new Dictionary<string, object>
            {
                ["category"] = Category,
                ["filename"] = Filename,
                ["section"] = Section,
                ["oldText"] = "drifts",
                ["newText"] = "hangs derelict",
                ["summary"] = Summary
            },
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
                documents.Verify(f => f.ReplaceSectionTextAsync(new(Category), Filename, Section, "drifts", "hangs derelict", Summary), Times.Once)),

        ["append_to_document"] = new ToolCase(
            Arguments: new Dictionary<string, object>
            {
                ["category"] = Category,
                ["filename"] = Filename,
                ["section"] = Section,
                ["text"] = Text
            },
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
                documents.Verify(f => f.AppendAsync(new(Category), Filename, Section, Text, null), Times.Once)),

        ["delete_document_section"] = new ToolCase(
            Arguments: new Dictionary<string, object>
            {
                ["category"] = Category,
                ["filename"] = Filename,
                ["section"] = Section
            },
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
                documents.Verify(f => f.DeleteSectionAsync(new(Category), Filename, Section, null), Times.Once)),

        ["archive_document"] = new ToolCase(
            Arguments: new Dictionary<string, object>
            {
                ["category"] = Category,
                ["filename"] = Filename
            },
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
                documents.Verify(f => f.DeleteDocumentAsync(new(Category), Filename), Times.Once)),

        ["get_document"] = new ToolCase(
            Arguments: new Dictionary<string, object>
            {
                ["category"] = Category,
                ["filename"] = Filename
            },
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
                documents.Verify(f => f.GetDocumentAsync(new(Category), Filename), Times.Once)),

        ["get_document_summary"] = new ToolCase(
            Arguments: new Dictionary<string, object>
            {
                ["category"] = Category,
                ["filename"] = Filename
            },
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
                documents.Verify(f => f.GetDocumentSummaryAsync(new(Category), Filename), Times.Once)),

        ["list_documents"] = new ToolCase(
            Arguments: new Dictionary<string, object> { ["category"] = Category },
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
                documents.Verify(f => f.GetDocumentIndexAsync(new(Category)), Times.Once)),

        ["roll_dice"] = new ToolCase(
            Arguments: new Dictionary<string, object> { ["purpose"] = "Face Danger: a hit means I cross the gap" },
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
            {
                embeddings.VerifyNoOtherCalls();
                documents.VerifyNoOtherCalls();
            }),

        ["get_meters"] = new ToolCase(
            Arguments: new Dictionary<string, object> { ["campaign"] = Campaign, ["name"] = MeterName },
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
                meters.Verify(m => m.GetMeter(IsCampaign(), IsMeterName()), Times.Once)),

        ["update_meter"] = new ToolCase(
            Arguments: new Dictionary<string, object>
            {
                ["campaign"] = Campaign,
                ["name"] = MeterName,
                ["mode"] = "delta",
                ["value"] = -2
            },
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
                meters.Verify(m => m.AdjustMeter(IsCampaign(), IsMeterName(), -2), Times.Once)),

        ["create_meter"] = new ToolCase(
            Arguments: new Dictionary<string, object>
            {
                ["campaign"] = Campaign,
                ["name"] = MeterName,
                ["min"] = 0,
                ["max"] = 5,
                ["value"] = 5
            },
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
                meters.Verify(m => m.CreateMeter(IsCampaign(), IsMeterName(), 0, 5, 5), Times.Once)),

        ["remove_meter"] = new ToolCase(
            Arguments: new Dictionary<string, object> { ["campaign"] = Campaign, ["name"] = MeterName },
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
                meters.Verify(m => m.RemoveMeter(IsCampaign(), IsMeterName()), Times.Once)),

        ["get_tracks"] = new ToolCase(
            Arguments: new Dictionary<string, object> { ["campaign"] = Campaign, ["track"] = TrackArgument },
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
                tracks.Verify(t => t.GetTrack(IsCampaign(), IsTrackId()), Times.Once)),

        ["update_track"] = new ToolCase(
            Arguments: new Dictionary<string, object>
            {
                ["campaign"] = Campaign,
                ["track"] = TrackArgument,
                ["mode"] = "mark",
                ["value"] = 2
            },
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
                tracks.Verify(t => t.MarkTrack(IsCampaign(), IsTrackId(), 2), Times.Once)),

        ["create_track"] = new ToolCase(
            Arguments: new Dictionary<string, object>
            {
                ["campaign"] = Campaign,
                ["track"] = TrackArgument,
                ["description"] = TrackDescription,
                ["rank"] = "dangerous",
                ["ticks"] = 4
            },
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
                tracks.Verify(t => t.CreateTrack(IsCampaign(), IsTrackId(), TrackDescription, Rank.Dangerous, 4), Times.Once)),

        ["edit_track"] = new ToolCase(
            Arguments: new Dictionary<string, object>
            {
                ["campaign"] = Campaign,
                ["track"] = TrackArgument,
                ["rank"] = "epic"
            },
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
                tracks.Verify(t => t.EditTrack(IsCampaign(), IsTrackId(), description: null, Rank.Epic), Times.Once)),

        ["remove_track"] = new ToolCase(
            Arguments: new Dictionary<string, object> { ["campaign"] = Campaign, ["track"] = TrackArgument },
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
                tracks.Verify(t => t.RemoveTrack(IsCampaign(), IsTrackId()), Times.Once)),

        ["request_write_permission"] = new ToolCase(
            Arguments: new Dictionary<string, object> { ["category"] = Category },
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
                permissions.Verify(p => p.EnableWrite(new(Category)), Times.Once)),

        ["release_write_permission"] = new ToolCase(
            Arguments: new Dictionary<string, object> { ["category"] = Category },
            VerifyDispatch: (embeddings, documents, permissions, meters, tracks) =>
                permissions.Verify(p => p.DisableWrite(new(Category)), Times.Once))
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
        var meters = CreateMetersMock();
        var tracks = CreateTracksMock();
        var server = McpServerFactory.Create(
            embeddings.Object, documents.Object, CreateDiceRoller(), permissions.Object, meters.Object, tracks.Object);

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

        response.ShouldHaveSucceeded(
            because: "'{0}' is advertised by tools/list, so calling it by that name must resolve to a handler",
            toolName);

        toolCase.VerifyDispatch(embeddings, documents, permissions, meters, tracks);
    }

    [Fact]
    public void Server_WhenTwoToolsShareAName_ShouldRefuseToStart()
    {
        var first = new Mock<ITool>();
        first.Setup(t => t.Definition).Returns(new Tool { Name = "search_index" });
        var second = new Mock<ITool>();
        second.Setup(t => t.Definition).Returns(new Tool { Name = "search_index" });

        var construct = () => new McpServer([first.Object, second.Object], NullLogger<McpServer>.Instance);

        construct.Should().Throw<InvalidOperationException>()
            .WithMessage("*'search_index'*",
                because: "a second tool under one name would be advertised twice and only one of them could ever be called");
    }

    private static List<string> ListAdvertisedToolNames()
    {
        var server = McpServerFactory.Create(
            CreateEmbeddingsMock().Object,
            CreateDocumentsMock().Object,
            CreateDiceRoller(),
            CreateWritePermissionsMock().Object);

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

        mock.Setup(p => p.IsWriteEnabled(It.IsAny<Category>())).Returns(true);
        mock.Setup(p => p.EnableWrite(It.IsAny<Category>()));
        mock.Setup(p => p.DisableWrite(It.IsAny<Category>()));

        return mock;
    }

    private static CampaignName IsCampaign() => It.Is<CampaignName>(campaign => campaign.Value == Campaign);

    private static StateTrackingId IsMeterName() => It.Is<StateTrackingId>(name => name.Value == MeterName);

    private static Mock<IMeterService> CreateMetersMock()
    {
        var mock = new Mock<IMeterService>(MockBehavior.Strict);
        var meter = new Meter(new StateTrackingId(MeterName), value: 3, min: 0, max: 5);
        var change = Result<MeterChange, ErrorCode>.Succeed(new MeterChange(meter, Clamped: 0));

        mock.Setup(m => m.GetMeter(It.IsAny<CampaignName>(), It.IsAny<StateTrackingId>()))
            .ReturnsAsync(Result<Meter, ErrorCode>.Succeed(meter));
        mock.Setup(m => m.AdjustMeter(It.IsAny<CampaignName>(), It.IsAny<StateTrackingId>(), It.IsAny<int>()))
            .ReturnsAsync(change);
        mock.Setup(m => m.CreateMeter(It.IsAny<CampaignName>(), It.IsAny<StateTrackingId>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int>()))
            .ReturnsAsync(change);
        mock.Setup(m => m.RemoveMeter(It.IsAny<CampaignName>(), It.IsAny<StateTrackingId>()))
            .ReturnsAsync(Result<StateTrackingId, ErrorCode>.Succeed(meter.Name));

        return mock;
    }

    private static StateTrackingId IsTrackId() => It.Is<StateTrackingId>(track => track.Value == TrackArgument);

    private static Mock<ITrackService> CreateTracksMock()
    {
        var mock = new Mock<ITrackService>(MockBehavior.Strict);
        var track = new Track(new(TrackKind.Vow, "handle-the-plantation"), TrackDescription, Rank.Dangerous, ticks: 8);
        var change = Result<TrackChange, ErrorCode>.Succeed(new TrackChange(track, Clamped: 0));

        mock.Setup(t => t.GetTrack(It.IsAny<CampaignName>(), It.IsAny<StateTrackingId>()))
            .ReturnsAsync(Result<Track, ErrorCode>.Succeed(track));
        mock.Setup(t => t.MarkTrack(It.IsAny<CampaignName>(), It.IsAny<StateTrackingId>(), It.IsAny<int>()))
            .ReturnsAsync(change);
        mock.Setup(t => t.CreateTrack(It.IsAny<CampaignName>(), It.IsAny<StateTrackingId>(), It.IsAny<string>(), It.IsAny<Rank>(), It.IsAny<int>()))
            .ReturnsAsync(change);
        mock.Setup(t => t.EditTrack(It.IsAny<CampaignName>(), It.IsAny<StateTrackingId>(), It.IsAny<string?>(), It.IsAny<Rank?>()))
            .ReturnsAsync(Result<Track, ErrorCode>.Succeed(track));
        mock.Setup(t => t.RemoveTrack(It.IsAny<CampaignName>(), It.IsAny<StateTrackingId>()))
            .ReturnsAsync(Result<TrackId, ErrorCode>.Succeed(track.Id));

        return mock;
    }

    private static Mock<IEmbeddingsFacade> CreateEmbeddingsMock()
    {
        var mock = new Mock<IEmbeddingsFacade>(MockBehavior.Strict);

        mock.Setup(f => f.SearchAsync(It.IsAny<string>(), It.IsAny<Category>(), It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<SearchResult>());
        mock.Setup(f => f.RetrieveByIdsAsync(It.IsAny<int[]>()))
            .ReturnsAsync(Array.Empty<TextResult>());

        return mock;
    }

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
        mock.Setup(f => f.GetDocumentAsync(It.IsAny<Category>(), It.IsAny<string>()))
            .ReturnsAsync(new Document());
        mock.Setup(f => f.GetDocumentSummaryAsync(It.IsAny<Category>(), It.IsAny<string>()))
            .ReturnsAsync(new DocumentIndexEntry());
        mock.Setup(f => f.GetDocumentIndexAsync(It.IsAny<Category>()))
            .ReturnsAsync(new List<DocumentIndexEntry>());
        mock.Setup(f => f.GetSubcategoriesAsync(It.IsAny<Category>()))
            .ReturnsAsync(new List<string>());
        mock.Setup(f => f.GetAncestorsHoldingDocumentsAsync(It.IsAny<Category>()))
            .ReturnsAsync(new List<string>());
        mock.Setup(f => f.FindTextAsync(It.IsAny<Category>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .ReturnsAsync(new TextSearchResult(TotalMatches: 0, Truncated: false, Documents: []));

        return mock;
    }
}
