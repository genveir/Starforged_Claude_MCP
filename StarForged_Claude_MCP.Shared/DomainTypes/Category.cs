namespace StarForged_Claude_MCP.Shared.DomainTypes;

/// <summary>
/// Categories form a tree written as dotted paths: 'Campaign.Oracles' is a subcategory of 'Campaign'. There is
/// no table of categories; the tree is whatever the stored paths spell out. A category either holds documents
/// (a leaf) or has subcategories under it (a parent), never both.
/// </summary>
public class Category
{
    private readonly string _value;

    public Category(string category)
    {
        if (!IsWellFormed(category))
            throw new ArgumentException(
                $"Category '{category}' is not a well-formed category path: its levels are separated by '.', " +
                "and none of them can be empty or start or end with a space.");

        _value = category;
    }

    public static bool IsWellFormed(string category) =>
        category.Split('.').All(segment => !string.IsNullOrWhiteSpace(segment) && segment.Trim() == segment);

    public override int GetHashCode()
    {
        return _value.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        return obj is Category other && string.Equals(_value, other._value, StringComparison.OrdinalIgnoreCase);
    }

    public override string ToString()
    {
        return _value;
    }
}
