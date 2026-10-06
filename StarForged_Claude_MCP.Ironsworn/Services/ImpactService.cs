using StarForged_Claude_MCP.Database.Models;
using StarForged_Claude_MCP.Database.Repositories;
using StarForged_Claude_MCP.Ironsworn.Abstractions;
using StarForged_Claude_MCP.Ironsworn.DomainTypes;
using StarForged_Claude_MCP.Ironsworn.Errors;
using StarForged_Claude_MCP.Shared.Results;

namespace StarForged_Claude_MCP.Ironsworn.Services;

internal class ImpactService : IImpactService
{
    private readonly CampaignResolver _campaignResolver;
    private readonly ImpactRepository _impactRepository;

    public ImpactService(CampaignResolver campaignResolver, ImpactRepository impactRepository)
    {
        _campaignResolver = campaignResolver;
        _impactRepository = impactRepository;
    }

    public Task<Result<IReadOnlyList<Impact>, ErrorCode>> GetImpacts(CampaignName campaign) =>
        _campaignResolver.InCampaign(campaign, operation: AllImpacts);

    public Task<Result<IReadOnlyList<Impact>, ErrorCode>> GetImpacts(CampaignName campaign, StateTrackingId entity) =>
        _campaignResolver.InCampaign(campaign, operation: async campaignId =>
        {
            var rows = await _impactRepository.GetImpacts(campaignId, entity.Value);

            return Result<IReadOnlyList<Impact>, ErrorCode>.Succeed(rows.Select(ToImpact).ToList());
        });

    public Task<Result<IReadOnlyList<Impact>, ErrorCode>> MarkImpact(
        CampaignName campaign, StateTrackingId entity, string name) =>
        _campaignResolver.InCampaign(campaign, operation: async campaignId =>
        {
            await _impactRepository.InsertImpact(campaignId, entity.Value, name);

            return await AllImpacts(campaignId);
        });

    public Task<Result<IReadOnlyList<Impact>, ErrorCode>> ClearImpact(
        CampaignName campaign, StateTrackingId entity, string name) =>
        _campaignResolver.InCampaign(campaign, operation: async campaignId =>
        {
            await _impactRepository.DeleteImpact(campaignId, entity.Value, name);

            return await AllImpacts(campaignId);
        });

    private async Task<Result<IReadOnlyList<Impact>, ErrorCode>> AllImpacts(int campaignId)
    {
        var rows = await _impactRepository.GetImpacts(campaignId);

        return Result<IReadOnlyList<Impact>, ErrorCode>.Succeed(rows.Select(ToImpact).ToList());
    }

    private static Impact ToImpact(ImpactRow row) => new(new StateTrackingId(row.ImpactedEntity), row.Name);
}
