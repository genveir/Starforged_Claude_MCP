using FluentAssertions;
using StarForged_Claude_MCP.Database.DomainTypes;

namespace StarForged_Claude_MCP.Tests.Database;

public class CategoryPathTests
{
    [Fact]
    public void Ancestors_ShouldListEveryCategoryAbove_NearestTheRootFirst()
    {
        new CategoryPath("Campaign.Oracles.Moons").Ancestors().Should().Equal("Campaign", "Campaign.Oracles");
    }

    [Fact]
    public void Ancestors_OfATopLevelCategory_ShouldBeEmpty()
    {
        new CategoryPath("Campaign").Ancestors().Should().BeEmpty();
    }
}
