using Android.App;
using Android.Content;
using Android.Database;
using ZBoard.Ime;
using ZBoard.Settings;

namespace ZBoard.Stats;

/// <summary>
/// Schnittstelle des Statistik-AddOns: liefert die Tipp-Metadaten als Cursor an eine Auswertungs-App.
/// Lesen dürfen nur Apps mit derselben Signatur (Berechtigung de.zeriadtke.zboard.permission.READ_TYPING_STATS).
///
///   content://de.zeriadtke.zboard.stats/keystrokes?since=&lt;unix-ms&gt;
///   content://de.zeriadtke.zboard.stats/sessions?since=&lt;unix-ms&gt;
///
/// Ist die Statistik in zBoard ausgeschaltet, sind die Ergebnisse leer.
/// </summary>
[ContentProvider(
    new[] { "de.zeriadtke.zboard.stats" },
    Name = "de.zeriadtke.zboard.StatsProvider",
    Exported = true,
    ReadPermission = "de.zeriadtke.zboard.permission.READ_TYPING_STATS")]
public sealed class StatsProvider : ContentProvider
{
    public const string Authority = "de.zeriadtke.zboard.stats";
    public const string Permission = "de.zeriadtke.zboard.permission.READ_TYPING_STATS";

    static readonly string[] KeystrokeColumns = ["timestamp_ms", "category", "hold_ms", "flight_ms", "session_id"];

    static readonly string[] SessionColumns =
    [
        "session_id", "start_ms", "end_ms", "keystrokes", "chars_per_minute", "correction_rate",
        "pauses_over_2s", "suggestions_accepted", "autocorrections",
    ];

    public override bool OnCreate() => true;

    public override ICursor? Query(
        Android.Net.Uri uri, string[]? projection, string? selection, string[]? selectionArgs, string? sortOrder)
    {
        var context = Context!;
        long since = long.TryParse(uri.GetQueryParameter("since"), out var s) ? s : 0;
        bool enabled = new ZBoardSettings(context).StatsEnabled;
        string directory = ZBoardImeService.StatsDirectory(context);

        switch (uri.LastPathSegment)
        {
            case "keystrokes":
            {
                var cursor = new MatrixCursor(KeystrokeColumns);
                if (!enabled) return cursor;
                foreach (var k in StatsReader.ReadKeystrokes(directory, since))
                {
                    cursor.NewRow()!
                        .Add(Java.Lang.Long.ValueOf(k.TimestampMs))!
                        .Add(new Java.Lang.String(k.Category.ToString()))!
                        .Add(Java.Lang.Integer.ValueOf(k.HoldMs))!
                        .Add(k.FlightMs is { } f ? Java.Lang.Integer.ValueOf(f) : null)!
                        .Add(new Java.Lang.String(k.SessionId.ToString()));
                }

                return cursor;
            }

            case "sessions":
            {
                var cursor = new MatrixCursor(SessionColumns);
                if (!enabled) return cursor;
                foreach (var x in StatsReader.ReadSessions(directory, since))
                {
                    cursor.NewRow()!
                        .Add(new Java.Lang.String(x.SessionId.ToString()))!
                        .Add(Java.Lang.Long.ValueOf(x.StartMs))!
                        .Add(Java.Lang.Long.ValueOf(x.EndMs))!
                        .Add(Java.Lang.Integer.ValueOf(x.Keystrokes))!
                        .Add(Java.Lang.Double.ValueOf(x.CharsPerMinute))!
                        .Add(Java.Lang.Double.ValueOf(x.CorrectionRate))!
                        .Add(Java.Lang.Integer.ValueOf(x.PausesOver2s))!
                        .Add(Java.Lang.Integer.ValueOf(x.SuggestionsAccepted))!
                        .Add(Java.Lang.Integer.ValueOf(x.Autocorrections));
                }

                return cursor;
            }

            default:
                return null;
        }
    }

    public override string? GetType(Android.Net.Uri uri) => uri.LastPathSegment switch
    {
        "keystrokes" => $"vnd.android.cursor.dir/vnd.{Authority}.keystroke",
        "sessions" => $"vnd.android.cursor.dir/vnd.{Authority}.session",
        _ => null,
    };

    // Nur lesend
    public override Android.Net.Uri? Insert(Android.Net.Uri uri, ContentValues? values) => null;

    public override int Delete(Android.Net.Uri uri, string? selection, string[]? selectionArgs) => 0;

    public override int Update(Android.Net.Uri uri, ContentValues? values, string? selection, string[]? selectionArgs) => 0;
}
