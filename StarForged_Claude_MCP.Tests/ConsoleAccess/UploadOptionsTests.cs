using FluentAssertions;
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
    [InlineData("lore", "--folder", @".\in", "--index")]
    [InlineData("lore", "--folder", @".\in", "--index", "--summaries", "none")]
    [InlineData("lore", "--folder", @".\in", "--index", "sometimes")]
    [InlineData("log", "--beats", "3", "--index", "all")]
    [InlineData("log", "--beats", "3", "--summaries", "none")]
    public void Parse_WithMissingOrInvalidArguments_ShouldReturnNull(params string[] args)
    {
        UploadOptions.Parse(args).Should().BeNull();
    }
}
