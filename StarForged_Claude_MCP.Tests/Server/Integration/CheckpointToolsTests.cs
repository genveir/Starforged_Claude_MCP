using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.Database.Repositories;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Ironsworn.DomainTypes;
using StarForged_Claude_MCP.Ironsworn.Errors;
using StarForged_Claude_MCP.Shared.Results;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

public class CheckpointToolsTests(TestFixture fixture) : McpServerTestBase(fixture)
{
    private const string Campaign = "Iron Expanse";
    private const string OtherCampaign = "Forge Drift";

    private CampaignRepository Campaigns => _fixture.Services.GetRequiredService<CampaignRepository>();
    private ICheckpointService Checkpoints => _fixture.Services.GetRequiredService<ICheckpointService>();

    [Fact]
    public async Task CreateCheckpoint_ShouldReturnTheNameAsSent_AndSaveACheckpointThatCanBeRestored()
    {
        await SetUpCampaigns(Campaign);

        var response = await CallTool("create", "create_checkpoint", Arguments(Campaign, "Session-7"));

        response.ShouldHaveSucceeded();
        ToolPayload(response).GetRawText().Should().Be("""{"checkpoint":"Session-7"}""");
        (await Checkpoints.RestoreCheckpoint(new CampaignName(Campaign), new StateTrackingId("session-7")))
            .Should().BeOfType<SuccessResult<StateTrackingId, ErrorCode>>();
    }

    [Fact]
    public async Task CreateCheckpoint_WithAnUnknownCampaign_ShouldSayTheCampaignDoesNotExist()
    {
        await SetUpCampaigns(Campaign);

        var response = await CallTool("unknown-campaign", "create_checkpoint", Arguments(OtherCampaign, "session-7"));

        response.ShouldHaveBeenRefused().Should().Contain($"No campaign named '{OtherCampaign}' exists");
    }

    [Fact]
    public async Task CreateCheckpoint_WithAMalformedName_ShouldBeRefused()
    {
        await SetUpCampaigns(Campaign);

        var response = await CallTool("malformed", "create_checkpoint", Arguments(Campaign, "session 7"));

        response.ShouldHaveBeenRefused().Should().Contain("'session 7' is not well-formed");
    }

    private async Task SetUpCampaigns(params string[] campaigns)
    {
        await _fixture.ClearCampaigns();
        foreach (var campaign in campaigns)
            await Campaigns.CreateCampaign(campaign);
    }

    private static Dictionary<string, object> Arguments(string campaign, string name) => new()
    {
        ["campaign"] = campaign,
        ["name"] = name
    };
}
