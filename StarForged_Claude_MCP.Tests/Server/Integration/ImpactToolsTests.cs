using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.Database.Repositories;
using StarForged_Claude_MCP.Server.Models;
using System.Text.Json;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

public class ImpactToolsTests(TestFixture fixture) : McpServerTestBase(fixture)
{
    private const string Campaign = "Iron Expanse";
    private const string OtherCampaign = "Forge Drift";
    private const string Character = "character";
    private const string Starship = "jorran-hasfer";

    private CampaignRepository Campaigns => _fixture.Services.GetRequiredService<CampaignRepository>();

    [Fact]
    public async Task SetImpact_Marking_ShouldReturnAllOfTheCampaignsImpacts()
    {
        await SetUpCampaigns(Campaign);
        await SetImpact(Campaign, Starship, "battered", marked: true);

        var impacts = await SetImpact(Campaign, Character, "wounded", marked: true);

        impacts.GetRawText().Should().Be(
            """{"impacts":[{"entity":"character","name":"wounded"},{"entity":"jorran-hasfer","name":"battered"}]}""");
    }

    [Fact]
    public async Task SetImpact_MarkingAMarkedImpact_ShouldChangeNothing()
    {
        await SetUpCampaigns(Campaign);
        await SetImpact(Campaign, Character, "wounded", marked: true);

        var impacts = await SetImpact(Campaign, Character, "wounded", marked: true);

        impacts.GetRawText().Should().Be("""{"impacts":[{"entity":"character","name":"wounded"}]}""");
    }

    [Fact]
    public async Task SetImpact_Clearing_ShouldRemoveOnlyThatImpact()
    {
        await SetUpCampaigns(Campaign);
        await SetImpact(Campaign, Character, "wounded", marked: true);
        await SetImpact(Campaign, Character, "shaken", marked: true);

        var impacts = await SetImpact(Campaign, Character, "wounded", marked: false);

        impacts.GetRawText().Should().Be("""{"impacts":[{"entity":"character","name":"shaken"}]}""");
        (await GetImpacts(new() { ["campaign"] = Campaign })).Should().ContainSingle();
    }

    [Fact]
    public async Task SetImpact_ClearingAnUnmarkedImpact_ShouldChangeNothing()
    {
        await SetUpCampaigns(Campaign);
        await SetImpact(Campaign, Character, "shaken", marked: true);

        var impacts = await SetImpact(Campaign, Character, "wounded", marked: false);

        impacts.GetRawText().Should().Be("""{"impacts":[{"entity":"character","name":"shaken"}]}""");
    }

    [Fact]
    public async Task GetImpacts_WithoutAnEntity_ShouldListAllImpactsOrderedByEntityThenName()
    {
        await SetUpCampaigns(Campaign);
        await SetImpact(Campaign, Starship, "battered", marked: true);
        await SetImpact(Campaign, Character, "wounded", marked: true);
        await SetImpact(Campaign, "research-lab", "broken", marked: true);
        await SetImpact(Campaign, Character, "shaken", marked: true);

        var impacts = await GetImpacts(new() { ["campaign"] = Campaign });

        impacts.Select(Describe).Should().Equal(
            "character/shaken", "character/wounded", "jorran-hasfer/battered", "research-lab/broken");
    }

    [Fact]
    public async Task GetImpacts_WithAnEntity_ShouldListOnlyItsImpactsOrderedByName()
    {
        await SetUpCampaigns(Campaign);
        await SetImpact(Campaign, Character, "wounded", marked: true);
        await SetImpact(Campaign, Starship, "battered", marked: true);
        await SetImpact(Campaign, Character, "shaken", marked: true);

        var impacts = await GetImpacts(new() { ["campaign"] = Campaign, ["entity"] = Character });

        impacts.Select(Describe).Should().Equal("character/shaken", "character/wounded");
    }

    [Fact]
    public async Task GetImpacts_WhenTheCampaignHasNone_ShouldReturnAnEmptyList()
    {
        await SetUpCampaigns(Campaign);

        var payload = ToolPayload(await CallSucceeding("get_impacts", new() { ["campaign"] = Campaign }));

        payload.GetRawText().Should().Be("""{"impacts":[]}""");
    }

    [Fact]
    public async Task SetImpact_WithANameWithSpaces_ShouldStoreItTrimmed()
    {
        await SetUpCampaigns(Campaign);

        var impacts = await SetImpact(Campaign, Character, "  permanently harmed ", marked: true);

        impacts.GetRawText().Should().Be("""{"impacts":[{"entity":"character","name":"permanently harmed"}]}""");
    }

    [Fact]
    public async Task Impacts_ShouldBeMatchedIgnoringCase_AndKeepTheCasingTheyWereMarkedWith()
    {
        await SetUpCampaigns(Campaign);
        await SetImpact(Campaign, Character, "wounded", marked: true);

        var remarked = await SetImpact(Campaign, "Character", "Wounded", marked: true);
        var byEntity = await GetImpacts(new() { ["campaign"] = Campaign, ["entity"] = "Character" });
        var cleared = await SetImpact(Campaign, Character, "WOUNDED", marked: false);

        remarked.GetRawText().Should().Be("""{"impacts":[{"entity":"character","name":"wounded"}]}""");
        byEntity.Select(Describe).Should().Equal("character/wounded");
        cleared.GetRawText().Should().Be("""{"impacts":[]}""");
    }

    [Fact]
    public async Task Impacts_WithTheSameNameOnTwoEntities_ShouldBeIndependent()
    {
        await SetUpCampaigns(Campaign);
        await SetImpact(Campaign, Starship, "battered", marked: true);
        await SetImpact(Campaign, "research-lab", "battered", marked: true);

        var impacts = await SetImpact(Campaign, Starship, "battered", marked: false);

        impacts.GetRawText().Should().Be("""{"impacts":[{"entity":"research-lab","name":"battered"}]}""");
    }

    [Fact]
    public async Task Impacts_InTwoCampaigns_ShouldBeIndependent()
    {
        await SetUpCampaigns(Campaign, OtherCampaign);
        await SetImpact(Campaign, Character, "wounded", marked: true);
        await SetImpact(OtherCampaign, Character, "wounded", marked: true);

        await SetImpact(OtherCampaign, Character, "wounded", marked: false);

        (await GetImpacts(new() { ["campaign"] = Campaign })).Select(Describe).Should().Equal("character/wounded");
        (await GetImpacts(new() { ["campaign"] = OtherCampaign })).Should().BeEmpty();
    }

    [Theory]
    [InlineData("get_impacts")]
    [InlineData("set_impact")]
    public async Task ImpactTool_WithAnUnknownCampaign_ShouldSayTheCampaignDoesNotExist(string toolName)
    {
        await SetUpCampaigns(Campaign);

        var response = await CallTool("unknown-campaign", toolName, SetArguments(OtherCampaign, Character, "wounded", marked: true));

        response.ShouldHaveBeenRefused().Should().Contain($"No campaign named '{OtherCampaign}' exists");
    }

    [Theory]
    [InlineData("get_impacts")]
    [InlineData("set_impact")]
    public async Task ImpactTool_WithAMalformedEntity_ShouldBeRefused(string toolName)
    {
        await SetUpCampaigns(Campaign);

        var response = await CallTool("malformed", toolName, SetArguments(Campaign, "the ship", "battered", marked: true));

        response.ShouldHaveBeenRefused().Should().Contain("'the ship' is not well-formed");
        (await GetImpacts(new() { ["campaign"] = Campaign })).Should().BeEmpty();
    }

    private async Task SetUpCampaigns(params string[] campaigns)
    {
        await _fixture.ClearCampaigns();
        foreach (var campaign in campaigns)
            await Campaigns.CreateCampaign(campaign);
    }

    private async Task<JsonElement> SetImpact(string campaign, string entity, string name, bool marked) =>
        ToolPayload(await CallSucceeding("set_impact", SetArguments(campaign, entity, name, marked)));

    private async Task<List<JsonElement>> GetImpacts(Dictionary<string, object> arguments) =>
        ToolPayload(await CallSucceeding("get_impacts", arguments)).GetProperty("impacts").EnumerateArray().ToList();

    private async Task<JsonRpcResponse> CallSucceeding(string toolName, Dictionary<string, object> arguments)
    {
        var response = await CallTool(toolName, toolName, arguments);
        response.ShouldHaveSucceeded();
        return response;
    }

    private static string Describe(JsonElement impact) =>
        $"{impact.GetProperty("entity").GetString()}/{impact.GetProperty("name").GetString()}";

    private static Dictionary<string, object> SetArguments(string campaign, string entity, string name, bool marked) => new()
    {
        ["campaign"] = campaign,
        ["entity"] = entity,
        ["name"] = name,
        ["marked"] = marked
    };
}
