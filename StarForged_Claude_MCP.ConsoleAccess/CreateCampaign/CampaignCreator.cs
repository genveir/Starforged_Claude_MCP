using StarForged_Claude_MCP.Database.Repositories;

namespace StarForged_Claude_MCP.ConsoleAccess.CreateCampaign;

public class CampaignCreator
{
    private readonly CampaignRepository campaigns;

    public CampaignCreator(CampaignRepository campaigns)
    {
        this.campaigns = campaigns;
    }

    public async Task Create(CreateCampaignOptions options)
    {
        var existingId = await campaigns.GetCampaignByName(options.Name);

        if (existingId != null)
        {
            Console.Error.WriteLine($"A campaign named '{options.Name}' already exists.");
            return;
        }

        var id = await campaigns.CreateCampaign(options.Name);

        Console.WriteLine($"Created campaign '{options.Name}' (id {id}).");
    }
}
