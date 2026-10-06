using StarForged_Claude_MCP.Database.Repositories;
using StarForged_Claude_MCP.Ironsworn.DomainTypes;

namespace StarForged_Claude_MCP.Ironsworn.Services;

internal class CampaignResolver
{
    private readonly CampaignRepository _campaignRepository;

    public CampaignResolver(CampaignRepository campaignRepository)
    {
        _campaignRepository = campaignRepository;
    }

    public async Task<int?> ResolveCampaignIdByName(CampaignName campaignName)
    {
        var campaignId = await _campaignRepository.GetCampaignByName(campaignName.Value);

        return campaignId;
    }
}
