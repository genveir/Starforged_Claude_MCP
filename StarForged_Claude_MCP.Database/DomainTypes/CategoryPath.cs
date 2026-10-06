using Dapper;

namespace StarForged_Claude_MCP.Database.DomainTypes;

public class CategoryPath
{
    public string Value { get; }

    public CategoryPath(string category)
    {
        Value = category;
    }

    /// <summary>
    /// Every category above this one, nearest the root first: 'A.B.C' gives 'A' and 'A.B'.
    /// </summary>
    public List<string> Ancestors()
    {
        var ancestors = new List<string>();

        for (var index = Value.IndexOf('.'); index >= 0; index = Value.IndexOf('.', index + 1))
            ancestors.Add(Value[..index]);

        return ancestors;
    }

    /// <summary>
    /// The start every path strictly under this category shares.
    /// </summary>
    internal string DescendantPrefix() => Value + '.';

    /// <summary>
    /// Matches a category and every category under it on the documents aliased 'd', given @Category and @Prefix
    /// as <see cref="InScopeParameters"/> binds them. The leading characters are compared rather than matched
    /// with LIKE, so that an '_' or '%' in a category name is never read as a wildcard.
    /// </summary>
    internal const string InScopeSql =
        "(d.Category = @Category or left(d.Category, len(@Prefix)) = @Prefix)";

    internal DynamicParameters InScopeParameters() =>
        new(new { Category = Value, Prefix = DescendantPrefix() });
}
