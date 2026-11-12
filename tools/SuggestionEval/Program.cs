using System.Globalization;
using ZBoard.Keyboard;
using ZBoard.Suggestions;
using ZBoard.Tools.SuggestionEval;

// Misst Wortvorschläge und Autokorrektur mit simuliertem Tippen über einen Testtext. Aufruf und Kennzahlen: README.md.

var options = Options.Parse(args);
if (options == null) return 1;

string root = options.Root ?? FindRepositoryRoot()
    ?? throw new InvalidOperationException("zBoard-Ordner nicht gefunden; bitte --root angeben.");
string dictDir = Path.Combine(root, "Assets", "dict");
string textPath = options.Text ?? Path.Combine(root, "tools", "SuggestionEval", "samples", $"{options.Language}.txt");
if (!File.Exists(textPath))
{
    Console.Error.WriteLine($"Testtext nicht gefunden: {textPath} (eigenen Text mit --text angeben)");
    return 1;
}

WordDictionary dictionary;
using (var stream = File.OpenRead(Path.Combine(dictDir, $"{options.Language}.txt")))
{
    dictionary = WordDictionary.Load(stream, WordDictionary.ReferenceTotal);
}

var bigrams = BigramModel.Empty;
string bigramPath = Path.Combine(dictDir, $"{options.Language}.bigrams.txt");
if (File.Exists(bigramPath))
{
    using var stream = File.OpenRead(bigramPath);
    bigrams = BigramModel.Load(stream);
}

var layout = Layouts.Alphabet(options.Language, showLanguageKey: false);
var engine = new SuggestionEngine(dictionary, KeyProximity.FromLayout(layout), bigrams, new UserHistory());
var words = Corpus.Load(textPath);
if (words.Count == 0)
{
    Console.Error.WriteLine("Der Testtext enthält keine Wörter.");
    return 1;
}

string? Context(TestWord word) => options.UseContext ? word.Previous : null;

// ---------- 1. Vorschläge bei fehlerfreiem Tippen ----------

int withContext = 0, predictedTop1 = 0, predictedTop3 = 0;
int halfWords = 0, halfTop1 = 0, halfTop3 = 0;
long keysNeeded = 0, keysBaseline = 0;
int unknown = 0;

foreach (var word in words)
{
    string target = word.Target, intended = word.Intended;
    if (!engine.InDictionary(target)) unknown++;

    int cost = intended.Length + 1; // ausschreiben und Leertaste
    if (word.Previous != null && options.UseContext)
    {
        withContext++;
        var predictions = engine.Predict(word.Previous, 3);
        if (predictions.Count > 0 && predictions[0] == target) predictedTop1++;
        if (predictions.Contains(target))
        {
            predictedTop3++;
            cost = 1; // antippen; das Leerzeichen kommt von selbst
        }
    }

    for (int typedLetters = 1; typedLetters < intended.Length; typedLetters++)
    {
        var bar = Bar.Of(engine.Suggest(intended[..typedLetters], Context(word)));
        bool visible = bar.Contains(target);
        if (visible && cost > typedLetters + 1) cost = typedLetters + 1;

        if (intended.Length >= 4 && typedLetters == (intended.Length + 1) / 2)
        {
            halfWords++;
            if (bar.Middle == target) halfTop1++;
            if (visible) halfTop3++;
        }
    }

    keysNeeded += cost;
    keysBaseline += intended.Length + 1;
}

// ---------- 2. Autokorrektur bei ungenauem Tippen ----------

var noisy = new Outcome();
var clean = new Outcome();
List<string> examples = [];

for (int run = 0; run < options.Runs; run++)
{
    var noise = new TypingNoise(layout, options.Seed + run) { Sigma = options.Sigma };
    foreach (var word in words)
    {
        string target = word.Target, intended = word.Intended;
        string typed = noise.Type(intended);
        bool typo = !string.Equals(typed, intended, StringComparison.OrdinalIgnoreCase);

        var result = engine.Suggest(typed, Context(word));
        string final = result.AutoCorrection ?? typed;
        var outcome = typo ? noisy : clean;
        outcome.Total++;

        if (final == target)
        {
            outcome.Correct++;
            continue;
        }

        string kind;
        if (string.Equals(final, target, StringComparison.OrdinalIgnoreCase)) (outcome.CaseOnly, kind) = (outcome.CaseOnly + 1, "Großschreibung");
        else if (Bar.Of(result).Contains(target)) (outcome.InBar, kind) = (outcome.InBar + 1, "in der Leiste");
        else if (final != typed) (outcome.WrongWord, kind) = (outcome.WrongWord + 1, "falsches Wort");
        else if (result.TypedIsWord) (outcome.RealWord, kind) = (outcome.RealWord + 1, "echtes Wort");
        else (outcome.Untouched, kind) = (outcome.Untouched + 1, "unverändert");

        if (options.Verbose && examples.Count < 400)
        {
            examples.Add($"{(typo ? "Tippfehler" : "richtig"),-10}  {kind,-14}  {word.Previous ?? "–",-14} {typed,-18} → {final,-18} (gewollt: {target})");
        }
    }
}

// ---------- Ausgabe ----------

// Deutsche Schreibweise von Hand, damit die Ausgabe nicht von den Spracheinstellungen des Rechners abhängt.
string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture).Replace('.', ',');
string Pct(long part, long total) => total == 0 ? "–" : Number(100.0 * part / total, "0.0") + " %";

int all = noisy.Total + clean.Total;
int wrongAfter = all - noisy.Correct - clean.Correct;
int wrongAfterIgnoringCase = wrongAfter - noisy.CaseOnly - clean.CaseOnly;

Console.WriteLine($"zBoard Vorschlagsmessung – Sprache {options.Language}, {words.Count} Wörter aus {Path.GetFileName(textPath)}");
Console.WriteLine($"Wörterbuch {dictionary.Count} Einträge, davon unbekannt im Text: {Pct(unknown, words.Count)}"
    + (options.UseContext ? "" : " – ohne Satzkontext"));
Console.WriteLine();
Console.WriteLine("| Vorschläge (fehlerfrei getippt) | Wert |");
Console.WriteLine("| --- | --- |");
Console.WriteLine($"| Nächstes Wort vorhergesagt, Platz 1 | {Pct(predictedTop1, withContext)} |");
Console.WriteLine($"| Nächstes Wort vorhergesagt, unter den ersten 3 | {Pct(predictedTop3, withContext)} |");
Console.WriteLine($"| Wort nach der Hälfte der Buchstaben in der Mitte der Leiste | {Pct(halfTop1, halfWords)} |");
Console.WriteLine($"| Wort nach der Hälfte der Buchstaben in der Leiste | {Pct(halfTop3, halfWords)} |");
Console.WriteLine($"| Gesparte Tastendrücke | {Pct(keysBaseline - keysNeeded, keysBaseline)} |");
Console.WriteLine();
Console.WriteLine($"| Autokorrektur ({options.Runs} Durchläufe, Streuung {Number(options.Sigma, "0.00")}) | Wert |");
Console.WriteLine("| --- | --- |");
Console.WriteLine($"| Wörter mit Tippfehler | {Pct(noisy.Total, all)} ({noisy.Total}) |");
Console.WriteLine($"| … davon richtig korrigiert | {Pct(noisy.Correct, noisy.Total)} |");
Console.WriteLine($"| … nur Groß-/Kleinschreibung falsch | {Pct(noisy.CaseOnly, noisy.Total)} |");
Console.WriteLine($"| … nicht korrigiert, richtiges Wort steht in der Leiste | {Pct(noisy.InBar, noisy.Total)} |");
Console.WriteLine($"| … in ein falsches Wort korrigiert | {Pct(noisy.WrongWord, noisy.Total)} |");
Console.WriteLine($"| … Tippfehler ergibt ein anderes echtes Wort | {Pct(noisy.RealWord, noisy.Total)} |");
Console.WriteLine($"| … unverändert stehen gelassen | {Pct(noisy.Untouched, noisy.Total)} |");
Console.WriteLine($"| Richtig getippte Wörter, die verändert wurden | {Pct(clean.WrongWord + clean.InBar, clean.Total)} |");
Console.WriteLine($"| Richtig getippte Wörter mit falscher Groß-/Kleinschreibung | {Pct(clean.CaseOnly, clean.Total)} |");
Console.WriteLine($"| **Falsche Wörter im Text nach der Korrektur** | **{Pct(wrongAfter, all)}** |");
Console.WriteLine($"| … ohne reine Groß-/Kleinschreibung | {Pct(wrongAfterIgnoringCase, all)} |");

if (options.Verbose)
{
    Console.WriteLine();
    Console.WriteLine("Fehlerbeispiele (Art, vorheriges Wort, getippt → Ergebnis):");
    foreach (var line in examples) Console.WriteLine("  " + line);
}

return 0;

static string? FindRepositoryRoot()
{
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Assets", "dict"))) return dir.FullName;
        }
    }

    return null;
}

namespace ZBoard.Tools.SuggestionEval
{
    /// <summary>Zählt, was aus den Wörtern einer Gruppe (mit oder ohne Tippfehler) nach dem Leerzeichen geworden ist.</summary>
    sealed class Outcome
    {
        public int Total, Correct, CaseOnly, InBar, WrongWord, RealWord, Untouched;
    }

    /// <summary>
    /// Was die Vorschlagsleiste zu einem Ergebnis zeigt – dieselbe Anordnung wie <c>UpdateSuggestions</c> im
    /// Tastaturdienst (ohne gelernte Wortfolgen): bei einer Autokorrektur links das Getippte, in der Mitte die
    /// Korrektur, rechts eine Alternative; sonst die drei besten Vorschläge, der beste in der Mitte.
    /// </summary>
    sealed record Bar(string? Left, string? Middle, string? Right)
    {
        public bool Contains(string word) => Left == word || Middle == word || Right == word;

        public static Bar Of(SuggestionResult result)
        {
            var candidates = result.Candidates;
            if (result.AutoCorrection is { } correction)
            {
                return new Bar(result.Typed, correction, candidates.FirstOrDefault(c => c != correction && c != result.Typed));
            }

            string? At(int i) => i < candidates.Count ? candidates[i] : null;
            string? left = !result.TypedIsWord && !candidates.Contains(result.Typed) ? result.Typed : At(1);
            return new Bar(left, At(0), At(2));
        }
    }

    sealed record Options(
        string Language, string? Text, string? Root, int Seed, int Runs, double Sigma, bool UseContext, bool Verbose)
    {
        public static Options? Parse(string[] args)
        {
            var options = new Options("de", null, null, Seed: 1, Runs: 5, Sigma: 0.24, UseContext: true, Verbose: false);
            for (int i = 0; i < args.Length; i++)
            {
                string? Value() => i + 1 < args.Length ? args[++i] : null;
                switch (args[i])
                {
                    case "--lang" when Value() is { } v: options = options with { Language = v }; break;
                    case "--text" when Value() is { } v: options = options with { Text = v }; break;
                    case "--root" when Value() is { } v: options = options with { Root = v }; break;
                    case "--seed" when Value() is { } v && int.TryParse(v, out var n): options = options with { Seed = n }; break;
                    case "--runs" when Value() is { } v && int.TryParse(v, out var n) && n > 0: options = options with { Runs = n }; break;
                    case "--sigma" when Value() is { } v
                        && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var s) && s >= 0:
                        options = options with { Sigma = s };
                        break;
                    case "--no-context": options = options with { UseContext = false }; break;
                    case "--verbose": options = options with { Verbose = true }; break;
                    default:
                        Console.Error.WriteLine(
                            """
                            Aufruf: dotnet run -c Release --project tools/SuggestionEval -- [Optionen]
                              --lang de|en|fr|es|it   Sprache (Wörterbuch, Wortpaare, Layout); Vorgabe de
                              --text <Datei>          eigener Testtext, ein Satz pro Zeile (Vorgabe: samples/<Sprache>.txt)
                              --runs <n>              Durchläufe mit unterschiedlichen Tippfehlern; Vorgabe 5
                              --sigma <x>             Streuung der Fingerposition in Tastenbreiten; Vorgabe 0.24
                              --seed <n>              Startwert des Zufallsgenerators; Vorgabe 1
                              --no-context            ohne vorheriges Wort messen
                              --verbose               Fehlerbeispiele ausgeben
                              --root <Ordner>         zBoard-Ordner, falls nicht von dort gestartet
                            """);
                        return null;
                }
            }

            return options;
        }
    }
}
