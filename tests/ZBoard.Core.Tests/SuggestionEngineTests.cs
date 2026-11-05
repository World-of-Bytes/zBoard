using ZBoard.Keyboard;
using ZBoard.Suggestions;
using Xunit;

namespace ZBoard.Core.Tests;

public class SuggestionEngineTests
{
    static SuggestionEngine CreateEngine()
    {
        var dictionary = new WordDictionary();
        dictionary.Add("hallo", 100);
        dictionary.Add("halle", 10);
        dictionary.Add("Haus", 80);
        dictionary.Add("haus", 5);
        dictionary.Add("für", 90);
        dictionary.Add("test", 70);
        dictionary.BuildTopLists();
        var proximity = KeyProximity.FromLayout(Layouts.Alphabet("de", showLanguageKey: false));
        return new SuggestionEngine(dictionary, proximity);
    }

    [Fact]
    public void Lookup_KeepsMoreFrequentSpelling()
    {
        var dictionary = new WordDictionary();
        dictionary.Add("Haus", 80);
        dictionary.Add("haus", 5);
        dictionary.BuildTopLists();

        Assert.Equal(("Haus", 80), dictionary.Lookup("haus"));
    }

    [Fact]
    public void Complete_ReturnsMostFrequentFirst()
    {
        var dictionary = new WordDictionary();
        dictionary.Add("hallo", 100);
        dictionary.Add("halle", 10);
        dictionary.Add("Haus", 80);
        dictionary.BuildTopLists();

        Assert.Equal("hallo", dictionary.Complete("ha")[0].Word);
    }

    [Fact]
    public void Autocorrects_NeighborKeyTypo()
    {
        var result = CreateEngine().Suggest("hallp");

        Assert.Equal("hallo", result.AutoCorrection);
    }

    [Fact]
    public void Autocorrects_MissingUmlaut()
    {
        var result = CreateEngine().Suggest("fur");

        Assert.Equal("für", result.AutoCorrection);
    }

    [Fact]
    public void KnownWord_IsNotCorrected_AndKeepsCase()
    {
        var result = CreateEngine().Suggest("Hallo");

        Assert.Null(result.AutoCorrection);
        Assert.True(result.TypedIsWord);
        Assert.Equal("Hallo", result.Candidates[0]);
    }

    [Fact]
    public void SuppressedWord_IsNotCorrectedAgain()
    {
        var engine = CreateEngine();
        engine.SuppressCorrection("hallp");

        Assert.Null(engine.Suggest("hallp").AutoCorrection);
    }

    [Fact]
    public void NounOnlyWord_IsCapitalized()
    {
        var dictionary = new WordDictionary();
        dictionary.Add("Straße", 50);
        dictionary.BuildTopLists();
        var engine = new SuggestionEngine(dictionary, KeyProximity.FromLayout(Layouts.Alphabet("de", false)));

        var result = engine.Suggest("straße");

        Assert.Equal("Straße", result.AutoCorrection);
    }

    [Fact]
    public void AmbiguousWord_KeepsTyped_AndOffersBothForms()
    {
        var dictionary = new WordDictionary();
        dictionary.Add("frage", 100);
        dictionary.Add("Frage", 60);
        dictionary.BuildTopLists();
        var engine = new SuggestionEngine(dictionary, KeyProximity.FromLayout(Layouts.Alphabet("de", false)));

        var result = engine.Suggest("frage");

        Assert.Null(result.AutoCorrection);
        Assert.Contains("frage", result.Candidates);
        Assert.Contains("Frage", result.Candidates);
    }

    [Fact]
    public void FindSimilar_SkipsHopelessPrefixes_ButFindsAllMatches()
    {
        var dictionary = new WordDictionary();
        foreach (var w in new[] { "haus", "hans", "maus", "laus", "hausen", "xylophon", "zebra", "aus" })
        {
            dictionary.Add(w, 10);
        }

        dictionary.BuildTopLists();

        var found = dictionary.FindSimilar("haus", 1f, (a, b) => 1f).Select(r => r.Word).OrderBy(w => w).ToArray();

        Assert.Equal(new[] { "aus", "hans", "haus", "laus", "maus" }, found);
    }

    [Theory]
    [InlineData("hallo", "HAL", "HALLO")]
    [InlineData("hallo", "Hal", "Hallo")]
    [InlineData("Haus", "ha", "Haus")]
    public void MatchCase_FollowsTypedCase(string word, string typed, string expected)
    {
        Assert.Equal(expected, SuggestionEngine.MatchCase(word, typed));
    }
}
