using ZBoard.Stats;
using Xunit;

namespace ZBoard.Core.Tests;

public class StatsRecorderTests
{
    sealed class MemorySink : IKeystrokeStatsSink
    {
        public List<KeystrokeEvent> Events { get; } = new();
        public List<SessionSummary> Summaries { get; } = new();

        public void Write(KeystrokeEvent keystroke) => Events.Add(keystroke);

        public void Write(SessionSummary summary) => Summaries.Add(summary);

        public void Flush()
        {
        }
    }

    // Uptime steht fest auf 1000 ms, Wanduhr auf 10 000 ms.
    static (KeystrokeStatsRecorder Recorder, MemorySink Sink) Create()
    {
        var sink = new MemorySink();
        return (new KeystrokeStatsRecorder(sink, () => 1000, () => 10_000), sink);
    }

    [Fact]
    public void RecordsHoldFlightAndWallClock()
    {
        var (recorder, sink) = Create();
        recorder.BeginSession();

        recorder.KeyDown(100);
        recorder.KeyUp(KeyCategory.Letter, 180);
        recorder.KeyDown(300);
        recorder.KeyUp(KeyCategory.Backspace, 350);

        Assert.Equal(80, sink.Events[0].HoldMs);
        Assert.Null(sink.Events[0].FlightMs);
        Assert.Equal(9180, sink.Events[0].TimestampMs);
        Assert.Equal(50, sink.Events[1].HoldMs);
        Assert.Equal(120, sink.Events[1].FlightMs);
    }

    [Fact]
    public void LongPause_ResetsFlight_AndCountsPause()
    {
        var (recorder, sink) = Create();
        recorder.BeginSession();

        recorder.KeyDown(100);
        recorder.KeyUp(KeyCategory.Letter, 150);
        recorder.KeyDown(6000);
        recorder.KeyUp(KeyCategory.Letter, 6050);
        recorder.EndSession();

        Assert.Null(sink.Events[1].FlightMs);
        Assert.Equal(1, sink.Summaries.Single().PausesOver2s);
    }

    [Fact]
    public void Summary_ComputesCorrectionRate()
    {
        var (recorder, sink) = Create();
        recorder.BeginSession();

        recorder.KeyDown(100);
        recorder.KeyUp(KeyCategory.Letter, 150);
        recorder.KeyDown(200);
        recorder.KeyUp(KeyCategory.Backspace, 250);
        recorder.EndSession();

        var summary = sink.Summaries.Single();
        Assert.Equal(2, summary.Keystrokes);
        Assert.Equal(0.5, summary.CorrectionRate);
    }

    [Fact]
    public void NothingIsRecorded_WithoutSession()
    {
        var (recorder, sink) = Create();

        recorder.KeyDown(100);
        recorder.KeyUp(KeyCategory.Letter, 150);

        Assert.Empty(sink.Events);
    }
}
