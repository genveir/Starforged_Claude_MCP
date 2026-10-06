using StarForged_Claude_MCP.Database.Repositories;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Ironsworn.DomainTypes;
using StarForged_Claude_MCP.Ironsworn.Errors;
using StarForged_Claude_MCP.Shared.Results;

namespace StarForged_Claude_MCP.Ironsworn.Services;

internal class CheckpointService : ICheckpointService
{
    private readonly CampaignResolver _campaignResolver;
    private readonly CheckpointRepository _checkpointRepository;

    public CheckpointService(CampaignResolver campaignResolver, CheckpointRepository checkpointRepository)
    {
        _campaignResolver = campaignResolver;
        _checkpointRepository = checkpointRepository;
    }

    public Task<Result<StateTrackingId, ErrorCode>> CreateCheckpoint(CampaignName campaign, StateTrackingId name) =>
        _campaignResolver.InCampaign(campaign, operation: async campaignId =>
        {
            await _checkpointRepository.SaveCheckpoint(campaignId, name.Value);

            return Result<StateTrackingId, ErrorCode>.Succeed(name);
        });

    public Task<Result<StateTrackingId, ErrorCode>> RestoreCheckpoint(CampaignName campaign, StateTrackingId name) =>
        _campaignResolver.InCampaign(campaign, operation: async campaignId =>
            await _checkpointRepository.RestoreCheckpoint(campaignId, name.Value)
                ? Result<StateTrackingId, ErrorCode>.Succeed(name)
                : Result<StateTrackingId, ErrorCode>.Fail(ErrorCode.Checkpoint_Not_Found));
}
