using FluentAssertions;
using StarForged_Claude_MCP.Shared.DomainTypes;

namespace StarForged_Claude_MCP.Tests.Database;

public class CategoryTests
{
    [Theory]
    [InlineData("Campaign")]
    [InlineData("Campaign.Oracles")]
    [InlineData("Campaign.Oracles.Moons")]
    [InlineData("Iron_Castle.Session Notes")]
    public void IsWellFormed_ForNonEmptyLevels_ShouldBeTrue(string category)
    {
        Category.IsWellFormed(category).Should().BeTrue();
    }

    [Theory]
    [InlineData("Campaign..Oracles")]
    [InlineData(".Campaign")]
    [InlineData("Campaign.")]
    [InlineData("Campaign. Oracles")]
    [InlineData("Campaign .Oracles")]
    public void IsWellFormed_ForAnEmptyOrPaddedLevel_ShouldBeFalse(string category)
    {
        Category.IsWellFormed(category).Should().BeFalse();
    }

    [Fact]
    public void Constructor_ForAMalformedCategory_ShouldThrow()
    {
        var construct = () => new Category("Campaign..Oracles");

        construct.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equals_ShouldIgnoreCase()
    {
        var category = new Category("Campaign.Oracles");
        var differentCase = new Category("campaign.ORACLES");

        category.Should().Be(differentCase);
        category.GetHashCode().Should().Be(differentCase.GetHashCode());
    }

    [Fact]
    public void ToString_ShouldGiveThePathAsWritten()
    {
        new Category("Campaign.Oracles").ToString().Should().Be("Campaign.Oracles");
    }
}
