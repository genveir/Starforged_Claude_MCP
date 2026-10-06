using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StarForged_Claude_MCP.Database.Models;
using StarForged_Claude_MCP.Database.Repositories;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Ironsworn.DomainTypes;
using StarForged_Claude_MCP.Ironsworn.Errors;
using StarForged_Claude_MCP.Shared.Results;

namespace StarForged_Claude_MCP.Tests.Server.Integration;

[Collection("McpServer")]
public class CheckpointServiceTests
{
    private const string Campaign = "Iron Expanse";
    private const string OtherCampaign = "Forge Drift";
    private const string Checkpoint = "session-7";

    private readonly TestFixture _fixture;
    private readonly CampaignRepository _campaigns;
    private readonly MeterRepository _meters;
    private readonly TrackRepository _tracks;
    private readonly ImpactRepository _impacts;
    private readonly ICheckpointService _checkpoints;

    public CheckpointServiceTests(TestFixture fixture)
    {
        _fixture = fixture;
        _campaigns = fixture.Services.GetRequiredService<CampaignRepository>();
        _meters = fixture.Services.GetRequiredService<MeterRepository>();
        _tracks = fixture.Services.GetRequiredService<TrackRepository>();
        _impacts = fixture.Services.GetRequiredService<ImpactRepository>();
        _checkpoints = fixture.Services.GetRequiredService<ICheckpointService>();
    }

    [Fact]
    public async Task RestoreCheckpoint_AfterPlay_ShouldPutBackExactlyTheStateAtTheCheckpoint()
    {
        var campaignId = await SetUpCampaignWithState(Campaign);
        var atCheckpoint = await ReadState(campaignId);
        await Create(Campaign, Checkpoint);

        await Play(campaignId);
        (await ReadState(campaignId)).Should().NotBeEquivalentTo(atCheckpoint);

        (await Restore(Campaign, Checkpoint)).Should().BeOfType<SuccessResult<StateTrackingId, ErrorCode>>()
            .Which.Value.Value.Should().Be(Checkpoint);
        (await ReadState(campaignId)).Should().BeEquivalentTo(atCheckpoint, config: options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task RestoreCheckpoint_Twice_ShouldWorkBothTimes_BecauseTheCheckpointIsKept()
    {
        var campaignId = await SetUpCampaignWithState(Campaign);
        var atCheckpoint = await ReadState(campaignId);
        await Create(Campaign, Checkpoint);

        await Play(campaignId);
        await Restore(Campaign, Checkpoint);
        await Play(campaignId);
        var restoredAgain = await Restore(Campaign, Checkpoint);

        restoredAgain.Should().BeOfType<SuccessResult<StateTrackingId, ErrorCode>>();
        (await ReadState(campaignId)).Should().BeEquivalentTo(atCheckpoint, config: options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task CreateCheckpoint_UnderAnExistingName_ShouldOverwriteIt()
    {
        var campaignId = await SetUpCampaignWithState(Campaign);
        await Create(Campaign, Checkpoint);
        await Play(campaignId);
        var newer = await ReadState(campaignId);

        await Create(Campaign, Checkpoint);
        await _meters.UpdateMeterValue(campaignId, "health", value: 0);
        await Restore(Campaign, Checkpoint);

        (await ReadState(campaignId)).Should().BeEquivalentTo(newer, config: options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Checkpoints_ShouldBeMatchedIgnoringCase()
    {
        var campaignId = await SetUpCampaignWithState(Campaign);
        var atCheckpoint = await ReadState(campaignId);
        await Create(Campaign, "Session-7");

        await Play(campaignId);
        var restored = await Restore(Campaign, "SESSION-7");

        restored.Should().BeOfType<SuccessResult<StateTrackingId, ErrorCode>>();
        (await ReadState(campaignId)).Should().BeEquivalentTo(atCheckpoint, config: options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task RestoreCheckpoint_WithAnUnknownName_ShouldFailWithCheckpointNotFound_AndChangeNothing()
    {
        var campaignId = await SetUpCampaignWithState(Campaign);
        await Create(Campaign, Checkpoint);
        await Play(campaignId);
        var afterPlay = await ReadState(campaignId);

        var restored = await Restore(Campaign, "session-8");

        restored.Should().BeOfType<FailureResult<StateTrackingId, ErrorCode>>()
            .Which.Error.Should().Be(ErrorCode.Checkpoint_Not_Found);
        (await ReadState(campaignId)).Should().BeEquivalentTo(afterPlay, config: options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task RestoreCheckpoint_OfAnotherCampaign_ShouldFailWithCheckpointNotFound()
    {
        await SetUpCampaignWithState(Campaign);
        await _campaigns.CreateCampaign(OtherCampaign);
        await Create(Campaign, Checkpoint);

        var restored = await Restore(OtherCampaign, Checkpoint);

        restored.Should().BeOfType<FailureResult<StateTrackingId, ErrorCode>>()
            .Which.Error.Should().Be(ErrorCode.Checkpoint_Not_Found);
    }

    [Fact]
    public async Task CheckpointOperations_WithAnUnknownCampaign_ShouldFailWithCampaignNotFound()
    {
        await _fixture.ClearCampaigns();

        var created = await Create(Campaign, Checkpoint);
        var restored = await Restore(Campaign, Checkpoint);

        created.Should().BeOfType<FailureResult<StateTrackingId, ErrorCode>>()
            .Which.Error.Should().Be(ErrorCode.Campaign_Not_Found);
        restored.Should().BeOfType<FailureResult<StateTrackingId, ErrorCode>>()
            .Which.Error.Should().Be(ErrorCode.Campaign_Not_Found);
    }

    [Fact]
    public async Task RestoreCheckpoint_ShouldLeaveAnotherCampaignsStateUntouched()
    {
        var campaignId = await SetUpCampaignWithState(Campaign);
        var otherId = await _campaigns.CreateCampaign(OtherCampaign);
        await _meters.InsertMeter(otherId, "health", value: 2, minValue: 0, maxValue: 5);
        await _tracks.InsertTrack(otherId, "vow", "find-the-signal", "dangerous", "Find the signal.", ticks: 4);
        await _impacts.InsertImpact(otherId, "character", "shaken");
        await Create(Campaign, Checkpoint);
        await Create(OtherCampaign, Checkpoint);
        await _meters.UpdateMeterValue(otherId, "health", value: 1);
        var other = await ReadState(otherId);

        await Play(campaignId);
        await Restore(Campaign, Checkpoint);

        (await ReadState(otherId)).Should().BeEquivalentTo(other, config: options => options.WithStrictOrdering());
    }

    private async Task<int> SetUpCampaignWithState(string campaign)
    {
        await _fixture.ClearCampaigns();
        var campaignId = await _campaigns.CreateCampaign(campaign);

        await _meters.InsertMeter(campaignId, "health", value: 5, minValue: 0, maxValue: 5);
        await _meters.InsertMeter(campaignId, "momentum", value: 2, minValue: -6, maxValue: 10);
        await _tracks.InsertTrack(campaignId, "vow", "handle-the-plantation", "dangerous", "Handle the drug plantation.", ticks: 8);
        await _tracks.InsertTrack(campaignId, "connection", "kira", "formidable", "Kira, a smuggler.", ticks: 0);
        await _impacts.InsertImpact(campaignId, "character", "wounded");
        await _impacts.InsertImpact(campaignId, "jorran-hasfer", "battered");

        return campaignId;
    }

    /// <summary>A session's worth of changes: values and ticks changed, items added, removed, cleared and marked.</summary>
    private async Task Play(int campaignId)
    {
        await _meters.UpdateMeterValue(campaignId, "health", value: 3);
        await _tracks.UpdateTrackTicks(campaignId, "vow", "handle-the-plantation", ticks: 16);
        await _meters.InsertMeter(campaignId, "xp", value: 2, minValue: 0, maxValue: null);
        await _tracks.DeleteTrack(campaignId, "connection", "kira");
        await _impacts.DeleteImpact(campaignId, "character", "wounded");
        await _impacts.InsertImpact(campaignId, "character", "shaken");
    }

    private async Task<CampaignState> ReadState(int campaignId) => new()
    {
        Meters = await _meters.GetMeters(campaignId),
        Tracks = await _tracks.GetTracks(campaignId),
        Impacts = await _impacts.GetImpacts(campaignId)
    };

    private Task<Result<StateTrackingId, ErrorCode>> Create(string campaign, string name) =>
        _checkpoints.CreateCheckpoint(new CampaignName(campaign), new StateTrackingId(name));

    private Task<Result<StateTrackingId, ErrorCode>> Restore(string campaign, string name) =>
        _checkpoints.RestoreCheckpoint(new CampaignName(campaign), new StateTrackingId(name));

    /// <summary>A plain class rather than a record or tuple, so equivalence compares the rows member by member.</summary>
    private sealed class CampaignState
    {
        public required List<MeterRow> Meters { get; init; }
        public required List<TrackRow> Tracks { get; init; }
        public required List<ImpactRow> Impacts { get; init; }
    }
}
