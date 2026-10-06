using StarForged_Claude_MCP.Shared.DomainTypes;

namespace StarForged_Claude_MCP.Database.DomainTypes;

public static class CategoryExtensions
{
    public static CategoryPath ToCategoryPath(this Category category) => new(category.ToString());
}
