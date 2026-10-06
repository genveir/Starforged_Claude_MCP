namespace StarForged_Claude_MCP.Ironsworn.DomainTypes;

public class CampaignName
{
    public string Value { get; }

    public CampaignName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Campaign name cannot be null or whitespace.", nameof(value));

        if (value.Length > 30)
            throw new ArgumentException("Campaign name cannot exceed 30 characters.", nameof(value));

        if (value != value.Trim())
            throw new ArgumentException("Campaign name cannot start or end with a space.", nameof(value));

        Value = value;
    }
}
