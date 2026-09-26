using FluentAssertions;
using StarForged_Claude_MCP.Embeddings.Services.Preprocessing;

namespace StarForged_Claude_MCP.Tests.Embeddings;

public class TokenizerTests
{
    // The tokenizer library loops forever on a character outside its vocabulary, so these run on another
    // thread under a timeout: a regression fails the test rather than hanging the run.
    private const int TimeoutMs = 10_000;

    [Theory(Timeout = TimeoutMs)]
    [InlineData("Pokémon")]
    [InlineData("Crème Brulé")]
    [InlineData("Smørrebrød")]
    [InlineData("Bún bò Huế")]
    [InlineData("Straße and Æsir")]
    [InlineData("Dragons 🐉 ahead")]
    public async Task Tokenize_WithCharactersOutsideTheVocabulary_ShouldFinish(string text)
    {
        var tokens = await Task.Run(() => Tokenizer.Tokenize(text), TestContext.Current.CancellationToken);

        tokens.Should().NotBeEmpty();
    }

    [Fact(Timeout = TimeoutMs)]
    public async Task Tokenize_WithAnAccentedWord_ShouldMatchTheWordWithoutAccents()
    {
        var accented = await Task.Run(() => Tokenizer.Tokenize("Pokémon"), TestContext.Current.CancellationToken);

        accented.Should().Equal(Tokenizer.Tokenize("Pokemon"));
    }

    [Theory]
    [InlineData("Pokémon", "Pokemon")]
    [InlineData("Bún bò Huế", "Bun bo Hue")]
    [InlineData("Smørrebrød", "Sm rrebr d")]
    [InlineData("Dragons 🐉 ahead", "Dragons    ahead")]
    public void MakeTokenizable_ShouldStripAccentsAndBlankOutWhatCannotBeTokenized(string text, string expected)
    {
        Tokenizer.MakeTokenizable(text).Should().Be(expected);
    }

    [Fact]
    public void MakeTokenizable_ShouldLeaveAsciiTextAlone()
    {
        var ascii = new string([.. Enumerable.Range(' ', '~' - ' ' + 1).Select(code => (char)code)]);

        Tokenizer.MakeTokenizable(ascii).Should().Be(ascii);
    }
}
