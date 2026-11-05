using System.Text;
using ZBoard.Clipboard;
using ZBoard.Emoji;
using ZBoard.Keyboard;
using ZBoard.Stats;
using Xunit;

namespace ZBoard.Core.Tests;

public class EmojiTests
{
    static EmojiCatalog Catalog() => EmojiCatalog.Load(
        new MemoryStream(Encoding.UTF8.GetBytes("# Test\n0\t😀\t0\n1\t👍\t1\n2\t🐻\t0\n")),
        new MemoryStream(Encoding.UTF8.GetBytes(
            "# Test\n😀\tgrinsendes Gesicht\tfroh|lachen|Smiley\n👍\tDaumen hoch\tgut|super\n🐻\tBär\tBär|Tier\n")));

    [Theory]
    [InlineData("gut", "👍")]
    [InlineData("lach", "😀")]
    [InlineData("Daumen", "👍")]
    [InlineData("BÄR", "🐻")]
    public void Search_FindsByKeywordAndName(string query, string expected)
    {
        Assert.Equal(expected, Catalog().Search(query)[0]);
    }

    [Fact]
    public void Search_EmptyQuery_ReturnsNothing() => Assert.Empty(Catalog().Search("  "));

    [Fact]
    public void Groups_AreRead() => Assert.Single(Catalog().InGroup(2));

    [Fact]
    public void SkinTones_AddModifierAfterFirstCharacter()
    {
        var variants = EmojiCatalog.SkinToneVariants("👍");

        Assert.Equal(6, variants.Count);
        Assert.Equal("👍\U0001F3FB", variants[1]);
    }

    [Fact]
    public void SkinTones_DropVariationSelector()
    {
        Assert.Equal("☝\U0001F3FD", EmojiCatalog.SkinToneVariants("☝️")[3]);
    }

    [Fact]
    public void Recent_KeepsNewestFirst_AndRoundTrips()
    {
        var recent = new RecentEmoji(capacity: 2);
        recent.Use("😀");
        recent.Use("👍");
        recent.Use("😀");
        recent.Use("🐻");

        Assert.Equal(new[] { "🐻", "😀" }, recent.Items);
        Assert.Equal(new[] { "🐻", "😀" }, RecentEmoji.Deserialize(recent.Serialize()).Items);
    }
}

public class ClipboardTests
{
    [Fact]
    public void Latest_OnlyWhileFresh()
    {
        var history = new ClipboardHistory();
        history.Add("Hallo", nowMs: 1_000);

        Assert.Equal("Hallo", history.Latest(nowMs: 30_000, maxAgeMs: 90_000)?.Text);
        Assert.Null(history.Latest(nowMs: 200_000, maxAgeMs: 90_000));
    }

    [Fact]
    public void Unpinned_ExpireAfterAnHour_PinnedStay()
    {
        var history = new ClipboardHistory();
        history.Add("weg", 0);
        history.Add("bleibt", 0);
        history.SetPinned("bleibt", true);

        var items = history.Items(ClipboardHistory.ExpiryMs + 1);

        Assert.Equal(new[] { "bleibt" }, items.Select(i => i.Text));
    }

    [Fact]
    public void OnlyPinned_AreSerialized()
    {
        var history = new ClipboardHistory();
        history.Add("geheim", 0);
        history.Add("Adresse\nZeile 2", 0);
        history.SetPinned("Adresse\nZeile 2", true);

        var restored = ClipboardHistory.DeserializePinned(history.SerializePinned());

        Assert.Equal(new[] { "Adresse\nZeile 2" }, restored.Items(0).Select(i => i.Text));
    }

    [Fact]
    public void Duplicate_MovesToTop()
    {
        var history = new ClipboardHistory();
        history.Add("a", 1);
        history.Add("b", 2);
        history.Add("a", 3);

        Assert.Equal(new[] { "a", "b" }, history.Items(3).Select(i => i.Text));
    }
}

public class StatsReaderTests
{
    [Fact]
    public void WrittenEvents_CanBeReadBack()
    {
        var dir = Path.Combine(Path.GetTempPath(), "zboard-test-" + Guid.NewGuid());
        try
        {
            var sink = new JsonlStatsSink(dir);
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var recorder = new KeystrokeStatsRecorder(sink, () => 1000, () => now);
            recorder.BeginSession();
            recorder.KeyDown(100);
            recorder.KeyUp(KeyCategory.Letter, 180);
            recorder.KeyDown(300);
            recorder.KeyUp(KeyCategory.Backspace, 340);
            recorder.EndSession();

            var keys = StatsReader.ReadKeystrokes(dir, sinceMs: 0);
            var sessions = StatsReader.ReadSessions(dir, sinceMs: 0);

            Assert.Equal(new[] { KeyCategory.Letter, KeyCategory.Backspace }, keys.Select(k => k.Category));
            Assert.Equal(120, keys[1].FlightMs);
            Assert.Equal(2, sessions.Single().Keystrokes);
            Assert.Empty(StatsReader.ReadKeystrokes(dir, sinceMs: now + 10_000));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}

public class LayoutTests
{
    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    [InlineData("fr")]
    [InlineData("es")]
    [InlineData("it")]
    public void EveryLanguage_HasThreeLetterRowsAndBottomRow(string language)
    {
        Assert.Equal(4, Layouts.Alphabet(language, showLanguageKey: true).Rows.Count);
        Assert.Equal(5, Layouts.Alphabet(language, showLanguageKey: true, numberRow: true).Rows.Count);
    }

    [Fact]
    public void French_IsAzerty() => Assert.Equal("a", Layouts.Alphabet("fr", false).Rows[0].Keys[0].Label);

    [Fact]
    public void Spanish_HasEnye() => Assert.Contains(Layouts.Alphabet("es", false).Rows[1].Keys, k => k.Label == "ñ");

    [Fact]
    public void BottomRow_HasEmojiKey() =>
        Assert.Contains(Layouts.Alphabet("de", false).Rows[^1].Keys, k => k.Action == KeyAction.Emoji);
}
