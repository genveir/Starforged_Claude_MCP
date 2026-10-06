using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.Database.Repositories;
using StarForged_Claude_MCP.Server.Models;
using System.Text.Json;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

public class TrackToolsTests(TestFixture fixture) : McpServerTestBase(fixture)
{
    private const string Campaign = "Iron Expanse";
    private const string OtherCampaign = "Forge Drift";
    private const string Plantation = "vow.handle-the-plantation";
    private const string PlantationVow = "Handle the drug plantation.";

    private CampaignRepository Campaigns => _fixture.Services.GetRequiredService<CampaignRepository>();

    [Fact]
    public async Task CreateTrack_ThenGetTracks_ShouldReturnTheTrackRecord()
    {
        await SetUpCampaigns(Campaign);

        var created = await CreateTrack(Campaign, Plantation, "dangerous", ticks: 9);
        var fetched = await GetTrack(Campaign, Plantation);

        const string record =
            """{"track":"vow.handle-the-plantation","kind":"vow","description":"Handle the drug plantation.","rank":"dangerous","ticks":9,"boxes":2}""";
        created.GetRawText().Should().Be(record);
        fetched.GetRawText().Should().Be(record);
    }

    [Fact]
    public async Task CreateTrack_WithoutTicks_ShouldStartAtZero()
    {
        await SetUpCampaigns(Campaign);

        var created = await CreateTrack(Campaign, Plantation, "epic", ticks: null);

        created.GetProperty("ticks").GetInt32().Should().Be(0);
        created.GetProperty("boxes").GetInt32().Should().Be(0);
    }

    [Theory]
    [InlineData(-4, 0, 4)]
    [InlineData(43, 40, 3)]
    public async Task CreateTrack_WithTicksOutOfRange_ShouldClampThemAndSaySoOnlyThen(int ticks, int expectedTicks, int expectedClamped)
    {
        await SetUpCampaigns(Campaign);

        var created = await CreateTrack(Campaign, Plantation, "dangerous", ticks);

        created.GetProperty("ticks").GetInt32().Should().Be(expectedTicks);
        created.GetProperty("clamped").GetInt32().Should().Be(expectedClamped);
        (await GetTrack(Campaign, Plantation)).TryGetProperty("clamped", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("troublesome", 12)]
    [InlineData("dangerous", 8)]
    [InlineData("formidable", 4)]
    [InlineData("extreme", 2)]
    [InlineData("epic", 1)]
    public async Task UpdateTrack_Mark_ShouldAddTheRanksTicksPerMark(string rank, int ticksPerMark)
    {
        await SetUpCampaigns(Campaign);
        await CreateTrack(Campaign, Plantation, rank, ticks: null);

        var marked = await UpdateTrack(Campaign, Plantation, "mark", value: 2);

        marked.GetProperty("ticks").GetInt32().Should().Be(2 * ticksPerMark);
        marked.TryGetProperty("clamped", out _).Should().BeFalse();
        (await GetTrack(Campaign, Plantation)).GetProperty("ticks").GetInt32().Should().Be(2 * ticksPerMark);
    }

    [Theory]
    [InlineData("mark", -1, 8, null)]
    [InlineData("mark", 4, 40, 8)]
    [InlineData("mark", -3, 0, 8)]
    [InlineData("set", 30, 30, null)]
    [InlineData("set", 41, 40, 1)]
    [InlineData("set", -2, 0, 2)]
    public async Task UpdateTrack_ShouldStoreTheTicksKeptInRange(string mode, int value, int expectedTicks, int? expectedClamped)
    {
        await SetUpCampaigns(Campaign);
        await CreateTrack(Campaign, Plantation, "dangerous", ticks: 16);

        var updated = await UpdateTrack(Campaign, Plantation, mode, value);

        updated.GetProperty("ticks").GetInt32().Should().Be(expectedTicks);
        updated.GetProperty("boxes").GetInt32().Should().Be(expectedTicks / 4);
        if (expectedClamped is { } clamped)
            updated.GetProperty("clamped").GetInt32().Should().Be(clamped);
        else
            updated.TryGetProperty("clamped", out _).Should().BeFalse();

        (await GetTrack(Campaign, Plantation)).GetProperty("ticks").GetInt32().Should().Be(expectedTicks);
    }

    [Fact]
    public async Task GetTracks_WithoutTrackOrKind_ShouldListAllTracksOrderedByKindThenName()
    {
        await SetUpCampaigns(Campaign);
        await CreateTrack(Campaign, "vow.find-my-sister", "extreme", ticks: null);
        await CreateTrack(Campaign, "connection.jorran-hasfer", "formidable", ticks: null);
        await CreateTrack(Campaign, Plantation, "dangerous", ticks: null);
        await CreateTrack(Campaign, "combat.dock-ambush", "troublesome", ticks: null);

        var tracks = await GetAllTracks(new() { ["campaign"] = Campaign });

        tracks.Select(track => track.GetProperty("track").GetString()).Should().Equal(
            "combat.dock-ambush", "connection.jorran-hasfer", "vow.find-my-sister", "vow.handle-the-plantation");
    }

    [Fact]
    public async Task GetTracks_WithAKind_ShouldListOnlyThatKindOrderedByName()
    {
        await SetUpCampaigns(Campaign);
        await CreateTrack(Campaign, Plantation, "dangerous", ticks: null);
        await CreateTrack(Campaign, "connection.jorran-hasfer", "formidable", ticks: null);
        await CreateTrack(Campaign, "vow.find-my-sister", "extreme", ticks: null);

        var tracks = await GetAllTracks(new() { ["campaign"] = Campaign, ["kind"] = "Vow" });

        tracks.Select(track => track.GetProperty("track").GetString())
            .Should().Equal("vow.find-my-sister", "vow.handle-the-plantation");
    }

    [Fact]
    public async Task GetTracks_WhenTheCampaignHasNone_ShouldReturnAnEmptyList()
    {
        await SetUpCampaigns(Campaign);

        var payload = ToolPayload(await CallSucceeding("get_tracks", new() { ["campaign"] = Campaign }));

        payload.GetRawText().Should().Be("""{"tracks":[]}""");
    }

    [Fact]
    public async Task GetTracks_WithTrackAndKindTogether_ShouldBeRefused()
    {
        await SetUpCampaigns(Campaign);
        await CreateTrack(Campaign, Plantation, "dangerous", ticks: null);

        var response = await CallTool("both", "get_tracks", new()
        {
            ["campaign"] = Campaign,
            ["track"] = Plantation,
            ["kind"] = "vow"
        });

        response.ShouldHaveBeenRefused().Should().StartWith("Track and kind cannot be given together");
    }

    [Fact]
    public async Task EditTrack_WithADescription_ShouldReplaceOnlyTheDescription()
    {
        await SetUpCampaigns(Campaign);
        await CreateTrack(Campaign, Plantation, "dangerous", ticks: 12);

        var edited = ToolPayload(await CallSucceeding("edit_track", new()
        {
            ["campaign"] = Campaign,
            ["track"] = Plantation,
            ["description"] = "Burn the drug plantation."
        }));

        const string record =
            """{"track":"vow.handle-the-plantation","kind":"vow","description":"Burn the drug plantation.","rank":"dangerous","ticks":12,"boxes":3}""";
        edited.GetRawText().Should().Be(record);
        (await GetTrack(Campaign, Plantation)).GetRawText().Should().Be(record);
    }

    [Fact]
    public async Task EditTrack_WithARank_ShouldLeaveTheTicksAndMarkByTheNewRank()
    {
        await SetUpCampaigns(Campaign);
        await CreateTrack(Campaign, Plantation, "dangerous", ticks: 16);

        var edited = ToolPayload(await CallSucceeding("edit_track", new()
        {
            ["campaign"] = Campaign,
            ["track"] = Plantation,
            ["rank"] = "Formidable"
        }));
        var marked = await UpdateTrack(Campaign, Plantation, "mark", value: 1);

        edited.GetProperty("rank").GetString().Should().Be("formidable");
        edited.GetProperty("ticks").GetInt32().Should().Be(16);
        edited.GetProperty("description").GetString().Should().Be(PlantationVow);
        marked.GetProperty("ticks").GetInt32().Should().Be(20);
    }

    [Fact]
    public async Task EditTrack_WithNeitherDescriptionNorRank_ShouldBeRefused()
    {
        await SetUpCampaigns(Campaign);
        await CreateTrack(Campaign, Plantation, "dangerous", ticks: null);

        var response = await CallTool("neither", "edit_track", TrackArguments(Campaign, Plantation));

        response.ShouldHaveBeenRefused().Should().Be("Description or rank is required: give the one to change, or both.");
    }

    [Fact]
    public async Task RemoveTrack_ShouldReturnTheIdAndLeaveNoTrackBehind()
    {
        await SetUpCampaigns(Campaign);
        await CreateTrack(Campaign, Plantation, "dangerous", ticks: null);

        var removed = ToolPayload(await CallSucceeding("remove_track", TrackArguments(Campaign, Plantation)));
        var fetched = await CallTool("get", "get_tracks", TrackArguments(Campaign, Plantation));

        removed.GetRawText().Should().Be("""{"removed":"vow.handle-the-plantation"}""");
        fetched.ShouldHaveBeenRefused().Should().Contain($"No track '{Plantation}'");
    }

    [Theory]
    [InlineData("get_tracks")]
    [InlineData("update_track")]
    [InlineData("create_track")]
    [InlineData("edit_track")]
    [InlineData("remove_track")]
    public async Task TrackTool_WithAnUnknownCampaign_ShouldSayTheCampaignDoesNotExist(string toolName)
    {
        await SetUpCampaigns(Campaign);

        var response = await CallTool("unknown-campaign", toolName, AllArguments(OtherCampaign, Plantation));

        response.ShouldHaveBeenRefused().Should().Contain($"No campaign named '{OtherCampaign}' exists");
    }

    [Theory]
    [InlineData("get_tracks")]
    [InlineData("update_track")]
    [InlineData("create_track")]
    [InlineData("edit_track")]
    [InlineData("remove_track")]
    public async Task TrackTool_WithAnIdWithoutAKind_ShouldSayItNeedsOne(string toolName)
    {
        await SetUpCampaigns(Campaign);

        var response = await CallTool("no-kind", toolName, AllArguments(Campaign, "handle-the-plantation"));

        response.ShouldHaveBeenRefused().Should().Be(
            "Track 'handle-the-plantation' has no kind: a track id has to start with its kind and a dot, e.g. " +
            "'vow.handle-the-plantation'. Track kinds are vow, connection, expedition and combat.");
    }

    [Theory]
    [InlineData("get_tracks")]
    [InlineData("update_track")]
    [InlineData("create_track")]
    [InlineData("edit_track")]
    [InlineData("remove_track")]
    public async Task TrackTool_WithAnUnknownKind_ShouldNameIt(string toolName)
    {
        await SetUpCampaigns(Campaign);

        var response = await CallTool("unknown-kind", toolName, AllArguments(Campaign, "quest.handle-the-plantation"));

        response.ShouldHaveBeenRefused().Should()
            .Be("'quest' is not a track kind. Track kinds are vow, connection, expedition and combat.");
    }

    [Theory]
    [InlineData("get_tracks")]
    [InlineData("update_track")]
    [InlineData("edit_track")]
    [InlineData("remove_track")]
    public async Task TrackTool_WithAnUnknownTrack_ShouldSayTheTrackDoesNotExist(string toolName)
    {
        await SetUpCampaigns(Campaign);

        var response = await CallTool("unknown-track", toolName, AllArguments(Campaign, Plantation));

        response.ShouldHaveBeenRefused().Should().Be(
            $"No track '{Plantation}' exists in campaign '{Campaign}'. get_tracks lists the campaign's tracks.");
    }

    [Fact]
    public async Task CreateTrack_WhenTheIdIsTaken_ShouldSayItAlreadyExists()
    {
        await SetUpCampaigns(Campaign);
        await CreateTrack(Campaign, Plantation, "dangerous", ticks: 8);

        var response = await CallTool("duplicate", "create_track", CreateArguments(Campaign, Plantation, "epic", ticks: null));

        response.ShouldHaveBeenRefused().Should().Be(
            $"A track '{Plantation}' already exists in campaign '{Campaign}'. Use edit_track to change its " +
            "description or rank, update_track to change its progress, or remove_track to remove it first.");
        (await GetTrack(Campaign, Plantation)).GetProperty("ticks").GetInt32().Should().Be(8);
    }

    [Theory]
    [InlineData("create_track")]
    [InlineData("edit_track")]
    public async Task TrackTool_WithAnUnknownRank_ShouldNameTheAcceptedRanks(string toolName)
    {
        await SetUpCampaigns(Campaign);
        await CreateTrack(Campaign, "vow.find-my-sister", "extreme", ticks: null);

        var response = await CallTool("rank", toolName, new()
        {
            ["campaign"] = Campaign,
            ["track"] = "vow.find-my-sister",
            ["description"] = PlantationVow,
            ["rank"] = "deadly"
        });

        response.ShouldHaveBeenRefused().Should().Be(
            "Rank has to be \"troublesome\", \"dangerous\", \"formidable\", \"extreme\" or \"epic\", but \"deadly\" was sent.");
    }

    [Fact]
    public async Task GetTracks_WithAnUnknownKind_ShouldNameTheAcceptedKinds()
    {
        await SetUpCampaigns(Campaign);

        var response = await CallTool("kind", "get_tracks", new() { ["campaign"] = Campaign, ["kind"] = "quest" });

        response.ShouldHaveBeenRefused().Should().Be(
            "Kind has to be \"vow\", \"connection\", \"expedition\" or \"combat\", but \"quest\" was sent.");
    }

    [Fact]
    public async Task UpdateTrack_WithAnUnknownMode_ShouldNameTheAcceptedModes()
    {
        await SetUpCampaigns(Campaign);
        await CreateTrack(Campaign, Plantation, "dangerous", ticks: null);

        var response = await CallTool("mode", "update_track", new()
        {
            ["campaign"] = Campaign,
            ["track"] = Plantation,
            ["mode"] = "delta",
            ["value"] = 1
        });

        response.ShouldHaveBeenRefused().Should().Be("Mode has to be \"mark\" or \"set\", but \"delta\" was sent.");
    }

    [Fact]
    public async Task TrackIds_ShouldBeMatchedIgnoringCase_AndKeepTheirStoredCasing()
    {
        await SetUpCampaigns(Campaign);
        await CreateTrack(Campaign, "vow.foo", "dangerous", ticks: null);

        var fetched = await GetTrack(Campaign, "Vow.FOO");
        var updated = await UpdateTrack(Campaign, "Vow.FOO", "mark", value: 1);
        var duplicate = await CallTool("duplicate", "create_track", CreateArguments(Campaign, "VOW.foo", "epic", ticks: null));
        var removed = ToolPayload(await CallSucceeding("remove_track", TrackArguments(Campaign, "Vow.FOO")));

        fetched.GetProperty("track").GetString().Should().Be("vow.foo");
        updated.GetProperty("track").GetString().Should().Be("vow.foo");
        updated.GetProperty("ticks").GetInt32().Should().Be(8);
        duplicate.ShouldHaveBeenRefused().Should().Contain("already exists");
        removed.GetProperty("removed").GetString().Should().Be("vow.foo");
    }

    [Fact]
    public async Task Tracks_WithTheSameIdInTwoCampaigns_ShouldBeIndependent()
    {
        await SetUpCampaigns(Campaign, OtherCampaign);
        await CreateTrack(Campaign, Plantation, "dangerous", ticks: null);
        await CreateTrack(OtherCampaign, Plantation, "epic", ticks: 3);

        await UpdateTrack(Campaign, Plantation, "set", value: 20);
        await CallSucceeding("remove_track", TrackArguments(OtherCampaign, Plantation));

        (await GetTrack(Campaign, Plantation)).GetProperty("ticks").GetInt32().Should().Be(20);
        (await GetAllTracks(new() { ["campaign"] = OtherCampaign })).Should().BeEmpty();
    }

    [Fact]
    public async Task Tracks_WithTheSameNameUnderTwoKinds_ShouldBeIndependent()
    {
        await SetUpCampaigns(Campaign);
        await CreateTrack(Campaign, "vow.jorran-hasfer", "dangerous", ticks: null);
        await CreateTrack(Campaign, "connection.jorran-hasfer", "formidable", ticks: null);

        await UpdateTrack(Campaign, "connection.jorran-hasfer", "mark", value: 1);
        await CallSucceeding("remove_track", TrackArguments(Campaign, "vow.jorran-hasfer"));

        var tracks = await GetAllTracks(new() { ["campaign"] = Campaign });
        tracks.Should().ContainSingle()
            .Which.GetProperty("track").GetString().Should().Be("connection.jorran-hasfer");
        tracks[0].GetProperty("ticks").GetInt32().Should().Be(4);
    }

    private async Task SetUpCampaigns(params string[] campaigns)
    {
        await _fixture.ClearCampaigns();
        foreach (var campaign in campaigns)
            await Campaigns.CreateCampaign(campaign);
    }

    private async Task<JsonElement> CreateTrack(string campaign, string track, string rank, int? ticks) =>
        ToolPayload(await CallSucceeding("create_track", CreateArguments(campaign, track, rank, ticks)));

    private async Task<JsonElement> GetTrack(string campaign, string track) =>
        ToolPayload(await CallSucceeding("get_tracks", TrackArguments(campaign, track)));

    private async Task<List<JsonElement>> GetAllTracks(Dictionary<string, object> arguments) =>
        ToolPayload(await CallSucceeding("get_tracks", arguments)).GetProperty("tracks").EnumerateArray().ToList();

    private async Task<JsonElement> UpdateTrack(string campaign, string track, string mode, int value) =>
        ToolPayload(await CallSucceeding("update_track", new()
        {
            ["campaign"] = campaign,
            ["track"] = track,
            ["mode"] = mode,
            ["value"] = value
        }));

    private async Task<JsonRpcResponse> CallSucceeding(string toolName, Dictionary<string, object> arguments)
    {
        var response = await CallTool(toolName, toolName, arguments);
        response.ShouldHaveSucceeded();
        return response;
    }

    private static Dictionary<string, object> TrackArguments(string campaign, string track) =>
        new() { ["campaign"] = campaign, ["track"] = track };

    /// <summary>Every argument any track tool requires, so one call reaches each tool's service operation.</summary>
    private static Dictionary<string, object> AllArguments(string campaign, string track) => new()
    {
        ["campaign"] = campaign,
        ["track"] = track,
        ["mode"] = "mark",
        ["value"] = 1,
        ["description"] = PlantationVow,
        ["rank"] = "dangerous"
    };

    private static Dictionary<string, object> CreateArguments(string campaign, string track, string rank, int? ticks)
    {
        var arguments = new Dictionary<string, object>
        {
            ["campaign"] = campaign,
            ["track"] = track,
            ["description"] = PlantationVow,
            ["rank"] = rank
        };
        if (ticks is { } starting)
            arguments["ticks"] = starting;
        return arguments;
    }
}
