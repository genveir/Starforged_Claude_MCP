using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.Database.Repositories;
using StarForged_Claude_MCP.Server.Models;
using System.Text.Json;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

public class MeterToolsTests(TestFixture fixture) : McpServerTestBase(fixture)
{
    private const string Campaign = "Iron Expanse";
    private const string OtherCampaign = "Forge Drift";

    private CampaignRepository Campaigns => _fixture.Services.GetRequiredService<CampaignRepository>();

    [Fact]
    public async Task CreateMeter_ThenGetMeters_ShouldReturnTheMeterRecord()
    {
        await SetUpCampaigns(Campaign);

        var created = await CreateMeter(Campaign, "health", min: 0, max: 5, value: 5);
        var fetched = await GetMeter(Campaign, "health");

        created.GetRawText().Should().Be("""{"name":"health","value":5,"min":0,"max":5}""");
        fetched.GetRawText().Should().Be("""{"name":"health","value":5,"min":0,"max":5}""");
    }

    [Fact]
    public async Task CreateMeter_WithoutMax_ShouldLeaveMaxOutOfTheRecord()
    {
        await SetUpCampaigns(Campaign);

        await CreateMeter(Campaign, "xp", min: 0, max: null, value: 2);

        (await GetMeter(Campaign, "xp")).GetRawText().Should().Be("""{"name":"xp","value":2,"min":0}""");
    }

    [Fact]
    public async Task CreateMeter_WithAValueOutOfRange_ShouldClampItAndSaySoOnlyThen()
    {
        await SetUpCampaigns(Campaign);

        var created = await CreateMeter(Campaign, "momentum", min: -6, max: 10, value: 12);

        created.GetRawText().Should().Be("""{"name":"momentum","value":10,"min":-6,"max":10,"clamped":2}""");
        (await GetMeter(Campaign, "momentum")).TryGetProperty("clamped", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("delta", -2, 3, null)]
    [InlineData("delta", -9, 0, 4)]
    [InlineData("delta", 3, 5, 3)]
    [InlineData("set", 1, 1, null)]
    [InlineData("set", 7, 5, 2)]
    [InlineData("set", -1, 0, 1)]
    public async Task UpdateMeter_ShouldStoreTheValueKeptInRange(string mode, int value, int expectedValue, int? expectedClamped)
    {
        await SetUpCampaigns(Campaign);
        await CreateMeter(Campaign, "health", min: 0, max: 5, value: 5);

        var updated = ToolPayload(await CallSucceeding("update_meter", new()
        {
            ["campaign"] = Campaign,
            ["name"] = "health",
            ["mode"] = mode,
            ["value"] = value
        }));

        updated.GetProperty("value").GetInt32().Should().Be(expectedValue);
        if (expectedClamped is { } clamped)
            updated.GetProperty("clamped").GetInt64().Should().Be(clamped);
        else
            updated.TryGetProperty("clamped", out _).Should().BeFalse();

        (await GetMeter(Campaign, "health")).GetProperty("value").GetInt32().Should().Be(expectedValue);
    }

    [Fact]
    public async Task GetMeters_WithoutAName_ShouldListAllMetersOrderedByName()
    {
        await SetUpCampaigns(Campaign);
        await CreateMeter(Campaign, "supply", min: 0, max: 5, value: 3);
        await CreateMeter(Campaign, "health", min: 0, max: 5, value: 5);
        await CreateMeter(Campaign, "momentum", min: -6, max: 10, value: 2);

        var meters = await GetAllMeters(Campaign);

        meters.Select(meter => meter.GetProperty("name").GetString())
            .Should().Equal("health", "momentum", "supply");
    }

    [Fact]
    public async Task GetMeters_WithoutAName_WhenTheCampaignHasNone_ShouldReturnAnEmptyList()
    {
        await SetUpCampaigns(Campaign);

        var payload = ToolPayload(await CallSucceeding("get_meters", new() { ["campaign"] = Campaign }));

        payload.GetRawText().Should().Be("""{"meters":[]}""");
    }

    [Fact]
    public async Task RemoveMeter_ShouldReturnTheNameAndLeaveNoMeterBehind()
    {
        await SetUpCampaigns(Campaign);
        await CreateMeter(Campaign, "health", min: 0, max: 5, value: 5);

        var removed = ToolPayload(await CallSucceeding("remove_meter", MeterArguments(Campaign, "health")));
        var fetched = await CallTool("get", "get_meters", MeterArguments(Campaign, "health"));

        removed.GetRawText().Should().Be("""{"removed":"health"}""");
        fetched.ShouldHaveBeenRefused().Should().Contain("No meter named 'health'");
    }

    [Theory]
    [InlineData("get_meters")]
    [InlineData("update_meter")]
    [InlineData("create_meter")]
    [InlineData("remove_meter")]
    public async Task MeterTool_WithAnUnknownCampaign_ShouldSayTheCampaignDoesNotExist(string toolName)
    {
        await SetUpCampaigns(Campaign);

        var response = await CallTool("unknown-campaign", toolName, new()
        {
            ["campaign"] = OtherCampaign,
            ["name"] = "health",
            ["mode"] = "set",
            ["min"] = 0,
            ["value"] = 1
        });

        response.ShouldHaveBeenRefused().Should().Contain($"No campaign named '{OtherCampaign}' exists");
    }

    [Theory]
    [InlineData("get_meters")]
    [InlineData("update_meter")]
    [InlineData("remove_meter")]
    public async Task MeterTool_WithAnUnknownMeter_ShouldSayTheMeterDoesNotExist(string toolName)
    {
        await SetUpCampaigns(Campaign);

        var response = await CallTool("unknown-meter", toolName, new()
        {
            ["campaign"] = Campaign,
            ["name"] = "health",
            ["mode"] = "delta",
            ["value"] = 1
        });

        response.ShouldHaveBeenRefused().Should()
            .Be($"No meter named 'health' exists in campaign '{Campaign}'. get_meters lists the campaign's meters.");
    }

    [Fact]
    public async Task CreateMeter_WhenTheNameIsTaken_ShouldSayItAlreadyExists()
    {
        await SetUpCampaigns(Campaign);
        await CreateMeter(Campaign, "health", min: 0, max: 5, value: 5);

        var response = await CallTool("duplicate", "create_meter", CreateArguments(Campaign, "health", min: 0, max: 5, value: 1));

        response.ShouldHaveBeenRefused().Should().Be(
            $"A meter named 'health' already exists in campaign '{Campaign}'. " +
            "Use update_meter to change its value, or remove_meter to remove it first.");
        (await GetMeter(Campaign, "health")).GetProperty("value").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task CreateMeter_WithMaxBelowMin_ShouldQuoteBoth()
    {
        await SetUpCampaigns(Campaign);

        var response = await CallTool("range", "create_meter", CreateArguments(Campaign, "health", min: 5, max: 4, value: 5));

        response.ShouldHaveBeenRefused().Should().Contain("Max 4 is below min 5");
        (await GetAllMeters(Campaign)).Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateMeter_WithAnUnknownMode_ShouldNameTheAcceptedModes()
    {
        await SetUpCampaigns(Campaign);
        await CreateMeter(Campaign, "health", min: 0, max: 5, value: 5);

        var response = await CallTool("mode", "update_meter", new()
        {
            ["campaign"] = Campaign,
            ["name"] = "health",
            ["mode"] = "add",
            ["value"] = 1
        });

        response.ShouldHaveBeenRefused().Should().Be("Mode has to be \"delta\" or \"set\", but \"add\" was sent.");
    }

    [Fact]
    public async Task MeterNames_ShouldBeMatchedIgnoringCase_AndKeepTheirStoredCasing()
    {
        await SetUpCampaigns(Campaign);
        await CreateMeter(Campaign, "health", min: 0, max: 5, value: 5);

        var fetched = await GetMeter(Campaign, "Health");
        var updated = ToolPayload(await CallSucceeding("update_meter", new()
        {
            ["campaign"] = Campaign,
            ["name"] = "HEALTH",
            ["mode"] = "delta",
            ["value"] = -1
        }));
        var duplicate = await CallTool("duplicate", "create_meter", CreateArguments(Campaign, "Health", min: 0, max: 5, value: 5));
        var removed = ToolPayload(await CallSucceeding("remove_meter", MeterArguments(Campaign, "Health")));

        fetched.GetProperty("name").GetString().Should().Be("health");
        updated.GetProperty("name").GetString().Should().Be("health");
        updated.GetProperty("value").GetInt32().Should().Be(4);
        duplicate.ShouldHaveBeenRefused().Should().Contain("already exists");
        removed.GetProperty("removed").GetString().Should().Be("health");
    }

    [Fact]
    public async Task Meters_WithTheSameNameInTwoCampaigns_ShouldBeIndependent()
    {
        await SetUpCampaigns(Campaign, OtherCampaign);
        await CreateMeter(Campaign, "health", min: 0, max: 5, value: 5);
        await CreateMeter(OtherCampaign, "health", min: 0, max: 3, value: 2);

        await CallSucceeding("update_meter", new()
        {
            ["campaign"] = Campaign,
            ["name"] = "health",
            ["mode"] = "set",
            ["value"] = 1
        });
        await CallSucceeding("remove_meter", MeterArguments(OtherCampaign, "health"));

        (await GetMeter(Campaign, "health")).GetRawText().Should().Be("""{"name":"health","value":1,"min":0,"max":5}""");
        (await GetAllMeters(OtherCampaign)).Should().BeEmpty();
    }

    private async Task SetUpCampaigns(params string[] campaigns)
    {
        await _fixture.ClearCampaigns();
        foreach (var campaign in campaigns)
            await Campaigns.CreateCampaign(campaign);
    }

    private async Task<JsonElement> CreateMeter(string campaign, string name, int min, int? max, int value) =>
        ToolPayload(await CallSucceeding("create_meter", CreateArguments(campaign, name, min, max, value)));

    private async Task<JsonElement> GetMeter(string campaign, string name) =>
        ToolPayload(await CallSucceeding("get_meters", MeterArguments(campaign, name)));

    private async Task<List<JsonElement>> GetAllMeters(string campaign) =>
        ToolPayload(await CallSucceeding("get_meters", new() { ["campaign"] = campaign }))
            .GetProperty("meters").EnumerateArray().ToList();

    private async Task<JsonRpcResponse> CallSucceeding(string toolName, Dictionary<string, object> arguments)
    {
        var response = await CallTool(toolName, toolName, arguments);
        response.ShouldHaveSucceeded();
        return response;
    }

    private static Dictionary<string, object> MeterArguments(string campaign, string name) =>
        new() { ["campaign"] = campaign, ["name"] = name };

    private static Dictionary<string, object> CreateArguments(string campaign, string name, int min, int? max, int value)
    {
        var arguments = new Dictionary<string, object> { ["campaign"] = campaign, ["name"] = name, ["min"] = min, ["value"] = value };
        if (max is { } upper)
            arguments["max"] = upper;
        return arguments;
    }
}
