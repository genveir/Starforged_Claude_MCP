using FluentAssertions;
using StarForged_Claude_MCP.Embeddings.Database;

namespace StarForged_Claude_MCP.Tests.Embeddings;

public class CategoryPathTests
{
    [Theory]
    [InlineData("Campaign")]
    [InlineData("Campaign.Oracles")]
    [InlineData("Campaign.Oracles.Moons")]
    [InlineData("Iron_Castle.Session Notes")]
    public void IsWellFormed_ForNonEmptyLevels_ShouldBeTrue(string category)
    {
        CategoryPath.IsWellFormed(category).Should().BeTrue();
    }

    [Theory]
    [InlineData("Campaign..Oracles")]
    [InlineData(".Campaign")]
    [InlineData("Campaign.")]
    [InlineData("Campaign. Oracles")]
    [InlineData("Campaign .Oracles")]
    public void IsWellFormed_ForAnEmptyOrPaddedLevel_ShouldBeFalse(string category)
    {
        CategoryPath.IsWellFormed(category).Should().BeFalse();
    }

    [Fact]
    public void Ancestors_ShouldListEveryCategoryAbove_NearestTheRootFirst()
    {
        CategoryPath.Ancestors("Campaign.Oracles.Moons").Should().Equal("Campaign", "Campaign.Oracles");
    }

    [Fact]
    public void Ancestors_OfATopLevelCategory_ShouldBeEmpty()
    {
        CategoryPath.Ancestors("Campaign").Should().BeEmpty();
    }
}
