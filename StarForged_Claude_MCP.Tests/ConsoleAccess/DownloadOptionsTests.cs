using FluentAssertions;
using StarForged_Claude_MCP.ConsoleAccess;
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
    [InlineData("lore", @".\out", "--folder", "--dry-run")]
    [InlineData("lore", "ship.md", "--document", "ship.md", "-n")]
    public void Parse_DryRunFlag_ShouldSetDryRunInEveryMode(params string[] args)
    {
        DownloadOptions.Parse(args)!.DryRun.Should().BeTrue();
    }

    [Theory]
    [InlineData("--verbosity", "changed", Verbosity.Changed)]
    [InlineData("-v", "ALL", Verbosity.All)]
    public void Parse_Verbosity_ShouldTakeTheModeFromTheFlag(string flag, string mode, Verbosity expected)
    {
        DownloadOptions.Parse(["lore", @".\out", "--folder", flag, mode])!.Verbosity.Should().Be(expected);
    }

    [Theory]
    [InlineData("lore", "--folder")]
    [InlineData("lore", "ship.md", "--document", "ship.md", "--clean")]
    [InlineData("lore", @".\out")]
    [InlineData("lore", "ship.md", "--document")]
    [InlineData("lore", @".\out", "--folder", "--index")]
    [InlineData("lore", @".\out", "--folder", "--verbosity")]
    [InlineData("lore", @".\out", "--folder", "-v", "some")]
    public void Parse_WithMissingOrInvalidArguments_ShouldReturnNull(params string[] args)
    {
        DownloadOptions.Parse(args).Should().BeNull();
    }
}
