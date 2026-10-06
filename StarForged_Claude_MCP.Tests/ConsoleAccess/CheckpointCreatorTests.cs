using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.ConsoleAccess.Checkpoints;
using StarForged_Claude_MCP.Database.Repositories;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Ironsworn.DomainTypes;
using StarForged_Claude_MCP.Ironsworn.Errors;
using StarForged_Claude_MCP.Shared.Results;
using StarForged_Claude_MCP.Tests.Server.Integration;

namespace StarForged_Claude_MCP.Tests.ConsoleAccess;

public class CheckpointCreatorTests(TestFixture fixture) : McpServerTestBase(fixture)
{
    private const string Campaign = "Iron Expanse";

    private CampaignRepository Campaigns => _fixture.Services.GetRequiredService<CampaignRepository>();
    private ICheckpointService Checkpoints => _fixture.Services.GetRequiredService<ICheckpointService>();

    [Fact]
    public async Task Create_ForAnExistingCampaign_ShouldSaveTheCheckpointAndSaySo()
    {
        await _fixture.ClearCampaigns();
        await Campaigns.CreateCampaign(Campaign);

        var (output, error) = await CreateCapturingOutput(Campaign, "session-7");

        output.Should().Contain($"Saved checkpoint 'session-7' of campaign '{Campaign}'.");
        error.Should().BeEmpty();
        (await Checkpoints.RestoreCheckpoint(new CampaignName(Campaign), new StateTrackingId("session-7")))
            .Should().BeOfType<SuccessResult<StateTrackingId, ErrorCode>>();
    }

    [Fact]
    public async Task Create_ForAnUnknownCampaign_ShouldSayTheCampaignDoesNotExist()
    {
        await _fixture.ClearCampaigns();

        var (output, error) = await CreateCapturingOutput(Campaign, "session-7");

        error.Should().Contain($"No campaign named '{Campaign}' exists.");
        output.Should().BeEmpty();
    }

    private async Task<(string Output, string Error)> CreateCapturingOutput(string campaign, string name)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        Console.SetOut(output);
        Console.SetError(error);

        try
        {
            await new CheckpointCreator(Checkpoints).Create(
                new CreateCheckpointOptions(new CampaignName(campaign), new StateTrackingId(name)));
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        return (output.ToString(), error.ToString());
    }
}
