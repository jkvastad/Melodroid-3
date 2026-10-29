using AwesomeAssertions;
using Melodroid_3.Music;
using Melodroid_3.Output;

namespace Melodroid_3.Tests.Output;

public class ReaperMarkerCsvTests
{
    [Theory]
    [InlineData(0, 0.0)]      // tick 0 → 0 s
    [InlineData(480, 0.5)]    // one quarter at 120 BPM (500000 µs/qtr, 480 tpq) → 0.5 s
    [InlineData(960, 1.0)]    // two quarters → 1.0 s
    public void TicksToSeconds_converts_at_constant_tempo(long tick, double expectedSeconds)
    {
        var tempo = new List<(long, long)> { (0L, 500_000L) };

        ReaperMarkerCsv.TicksToSeconds(tick, ticksPerQuarter: 480, tempo)
            .Should().BeApproximately(expectedSeconds, 1e-9);
    }

    [Fact]
    public void TicksToSeconds_integrates_across_a_tempo_change()
    {
        // 120 BPM for the first quarter (480 ticks), then 60 BPM (1000000 µs/qtr) afterwards.
        var tempo = new List<(long, long)> { (0L, 500_000L), (480L, 1_000_000L) };

        // First quarter = 0.5 s; the next quarter at half speed = 1.0 s → 1.5 s total.
        ReaperMarkerCsv.TicksToSeconds(960, ticksPerQuarter: 480, tempo)
            .Should().BeApproximately(1.5, 1e-9);
    }

    [Fact]
    public void Build_emits_region_rows_for_scales_and_marker_rows_for_placements()
    {
        var tempo = new List<(long, long)> { (0L, 500_000L) };
        var scale = new LabelSegment(0, 1, StartBeat: 0.0, EndBeat: 2.0, Length: 2,
            Witnesses: new[] { "M@0", "8@5" });
        var placement = new LabelSegment(0, 0, StartBeat: 2.0, EndBeat: 3.0, Length: 1,
            Witnesses: new[] { "8@0" });

        var csv = ReaperMarkerCsv.Build(
            new[] { scale }, new[] { placement }, ticksPerQuarter: 480, tempo);

        csv.Should().Contain("#,Name,Start,End,Length,Color");
        // Scale region: space-joined witnesses, start 0:00.000, end 0:01.000 (2 beats @120 BPM).
        csv.Should().Contain("R1,M@0 8@5,0:00.000,0:01.000,0:01.000,");
        // Placement marker: start only (1.0 s), no end/length.
        csv.Should().Contain("M1,8@0,0:01.000,,,");
    }

    [Fact]
    public void Build_labels_empty_witness_segments_with_a_placeholder()
    {
        var tempo = new List<(long, long)> { (0L, 500_000L) };
        var silent = new LabelSegment(0, 0, StartBeat: 0.0, EndBeat: 1.0, Length: 1,
            Witnesses: Array.Empty<string>());

        var csv = ReaperMarkerCsv.Build(
            new[] { silent }, Array.Empty<LabelSegment>(), ticksPerQuarter: 480, tempo);

        csv.Should().Contain("R1,(none),");
    }
}
