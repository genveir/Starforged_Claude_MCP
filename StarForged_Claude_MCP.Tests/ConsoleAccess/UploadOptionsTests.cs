using FluentAssertions;
using StarForged_Claude_MCP.ConsoleAccess;
using StarForged_Claude_MCP.ConsoleAccess.Upload;

namespace StarForged_Claude_MCP.Tests.ConsoleAccess;

public class UploadOptionsTests
{
    [Fact]
    public void Parse_WithoutIndexOrSummaries_ShouldAskOnlyForWhatIsNew()
    {
        UploadOptions.Parse(["lore", "--folder", @".\in"]).Should()
            .Be(new UploadOptions("lore", UploadMode.Folder, @".\in", Index: IndexMode.New, Summaries: SummaryMode.Missing));
    }

    [Theory]
    [InlineData("--index", "all", IndexMode.All)]
    [InlineData("-i", "ASK", IndexMode.Ask)]
    [InlineData("--index", "new", IndexMode.New)]
    [InlineData("-i", "drop", IndexMode.Drop)]
    public void Parse_IndexMode_ShouldTakeTheModeFromTheFlag(string flag, string mode, IndexMode expected)
    {
        UploadOptions.Parse(["lore", "--document", "ship.md", flag, mode])!.Index.Should().Be(expected);
    }

    [Theory]
    [InlineData("--dry-run")]
    [InlineData("-n")]
    public void Parse_DryRunFlag_ShouldSetDryRun(string flag)
    {
        UploadOptions.Parse(["lore", "--folder", @".\in", flag])!.DryRun.Should().BeTrue();
    }

    [Theory]
    [InlineData("--verbosity", "changed", Verbosity.Changed)]
    [InlineData("-v", "ALL", Verbosity.All)]
    public void Parse_Verbosity_ShouldTakeTheModeFromTheFlag(string flag, string mode, Verbosity expected)
    {
        UploadOptions.Parse(["lore", "--folder", @".\in", flag, mode])!.Verbosity.Should().Be(expected);
    }

    [Theory]
    [InlineData("lore", "--folder", @".\in", "--index")]
    [InlineData("lore", "--folder", @".\in", "--index", "--summaries", "none")]
    [InlineData("lore", "--folder", @".\in", "--index", "sometimes")]
    [InlineData("log", "--beats", "3", "--index", "all")]
    [InlineData("log", "--beats", "3", "--summaries", "none")]
    [InlineData("log", "--beats", "3", "--dry-run")]
    [InlineData("log", "--beats", "3", "--verbosity", "changed")]
    [InlineData("lore", "--folder", @".\in", "-v", "some")]
    public void Parse_WithMissingOrInvalidArguments_ShouldReturnNull(params string[] args)
    {
        UploadOptions.Parse(args).Should().BeNull();
    }
}
