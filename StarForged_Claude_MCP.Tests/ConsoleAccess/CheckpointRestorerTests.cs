using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.ConsoleAccess.Checkpoints;
using StarForged_Claude_MCP.ConsoleAccess.Download;
using StarForged_Claude_MCP.Database.Repositories;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Ironsworn.DomainTypes;
using StarForged_Claude_MCP.Tests.Server.Integration;

namespace StarForged_Claude_MCP.Tests.ConsoleAccess;

public class CheckpointRestorerTests(TestFixture fixture) : McpServerTestBase(fixture)
{
    private const string Campaign = "Iron Expanse";
    private const string Checkpoint = "session-7";

    private readonly RecordingConfirmPrompt confirmPrompt = new();

    private CampaignRepository Campaigns => _fixture.Services.GetRequiredService<CampaignRepository>();
    private MeterRepository Meters => _fixture.Services.GetRequiredService<MeterRepository>();
    private ICheckpointService Checkpoints => _fixture.Services.GetRequiredService<ICheckpointService>();

    [Fact]
    public async Task Restore_WhenConfirmed_ShouldRestoreTheCheckpointAndSaySo()
    {
        var campaignId = await SetUpCampaignWithCheckpoint();
        await Meters.UpdateMeterValue(campaignId, "health", value: 1);
        confirmPrompt.Answer = true;

        var (output, error) = await RestoreCapturingOutput(Checkpoint, yes: false);

        confirmPrompt.Asked.Should().ContainSingle()
            .Which.Should().Contain($"campaign '{Campaign}'").And.Contain($"checkpoint '{Checkpoint}'");
        output.Should().Contain($"Restored checkpoint '{Checkpoint}' of campaign '{Campaign}'.");
        error.Should().BeEmpty();
        (await Meters.GetMeter(campaignId, "health"))!.Value.Should().Be(5);
    }

    [Fact]
    public async Task Restore_WhenDeclined_ShouldChangeNothing()
    {
        var campaignId = await SetUpCampaignWithCheckpoint();
        await Meters.UpdateMeterValue(campaignId, "health", value: 1);
        confirmPrompt.Answer = false;

        var (output, error) = await RestoreCapturingOutput(Checkpoint, yes: false);

        output.Should().Contain("Cancelled. Nothing was restored.");
        error.Should().BeEmpty();
        (await Meters.GetMeter(campaignId, "health"))!.Value.Should().Be(1);
    }

    [Fact]
    public async Task Restore_WithYes_ShouldRestoreWithoutAsking()
    {
        var campaignId = await SetUpCampaignWithCheckpoint();
        await Meters.UpdateMeterValue(campaignId, "health", value: 1);

        await RestoreCapturingOutput(Checkpoint, yes: true);

        confirmPrompt.Asked.Should().BeEmpty();
        (await Meters.GetMeter(campaignId, "health"))!.Value.Should().Be(5);
    }

    [Fact]
    public async Task Restore_WithAnUnknownCheckpoint_ShouldSaySo()
    {
        await SetUpCampaignWithCheckpoint();

        var (output, error) = await RestoreCapturingOutput("session-8", yes: true);

        error.Should().Contain($"Campaign '{Campaign}' has no checkpoint named 'session-8'.");
        output.Should().BeEmpty();
    }

    [Fact]
    public async Task Restore_WithAnUnknownCampaign_ShouldSaySo()
    {
        await _fixture.ClearCampaigns();

        var (output, error) = await RestoreCapturingOutput(Checkpoint, yes: true);

        error.Should().Contain($"No campaign named '{Campaign}' exists.");
        output.Should().BeEmpty();
    }

    private async Task<int> SetUpCampaignWithCheckpoint()
    {
        await _fixture.ClearCampaigns();
        var campaignId = await Campaigns.CreateCampaign(Campaign);
        await Meters.InsertMeter(campaignId, "health", value: 5, minValue: 0, maxValue: 5);
        await Checkpoints.CreateCheckpoint(new CampaignName(Campaign), new StateTrackingId(Checkpoint));

        return campaignId;
    }

    private async Task<(string Output, string Error)> RestoreCapturingOutput(string name, bool yes)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        Console.SetOut(output);
        Console.SetError(error);

        try
        {
            await new CheckpointRestorer(Checkpoints, confirmPrompt).Restore(
                new RestoreCheckpointOptions(new CampaignName(Campaign), new StateTrackingId(name), yes));
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        return (output.ToString(), error.ToString());
    }

    private sealed class RecordingConfirmPrompt : IConfirmPrompt
    {
        public bool Answer { get; set; }
        public List<string> Asked { get; } = [];

        public bool Confirm(string question)
        {
            Asked.Add(question);
            return Answer;
        }
    }
}
