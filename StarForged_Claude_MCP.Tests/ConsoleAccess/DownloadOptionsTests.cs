using FluentAssertions;
using StarForged_Claude_MCP.ConsoleAccess.Download;

namespace StarForged_Claude_MCP.Tests.ConsoleAccess;

public class DownloadOptionsTests
{
    [Fact]
    public void Parse_Folder_ShouldTakeCategoryAndPathPositionally()
    {
        DownloadOptions.Parse(["lore", @".\out", "--folder"]).Should()
            .Be(new DownloadOptions("lore", @".\out", DownloadMode.Folder));
    }

    [Fact]
    public void Parse_Document_ShouldTakeTheFilenameFromTheFlag()
    {
        DownloadOptions.Parse(["lore", "ship.md", "-d", "ship.md", "--overwrite"]).Should()
            .Be(new DownloadOptions("lore", "ship.md", DownloadMode.Document, Filename: "ship.md", Overwrite: true));
    }

    [Fact]
    public void Parse_Beats_ShouldTakeTheSessionNumberFromTheFlag()
    {
        DownloadOptions.Parse(["log", "s3.md", "--beats", "3"]).Should()
            .Be(new DownloadOptions("log", "s3.md", DownloadMode.Beats, SessionNumber: 3));
    }

    [Fact]
    public void Parse_ShortOverwriteFlag_ShouldSetOverwrite()
    {
        DownloadOptions.Parse(["lore", @".\out", "-f", "-o"])!.Overwrite.Should().BeTrue();
    }

    [Theory]
    [InlineData("--clean")]
    [InlineData("-c")]
    public void Parse_CleanFlagWithFolder_ShouldSetClean(string flag)
    {
        DownloadOptions.Parse(["lore", @".\out", "--folder", flag])!.Clean.Should().BeTrue();
    }

    [Theory]
    [InlineData("lore", "--folder")]
    [InlineData("lore", "ship.md", "--document", "ship.md", "--clean")]
    [InlineData("log", "s3.md", "--beats", "3", "-c")]
    [InlineData("lore", @".\out")]
    [InlineData("lore", "s3.md", "--beats", "three")]
    [InlineData("lore", "ship.md", "--document")]
    [InlineData("lore", @".\out", "--folder", "--index")]
    public void Parse_WithMissingOrInvalidArguments_ShouldReturnNull(params string[] args)
    {
        DownloadOptions.Parse(args).Should().BeNull();
    }
}
