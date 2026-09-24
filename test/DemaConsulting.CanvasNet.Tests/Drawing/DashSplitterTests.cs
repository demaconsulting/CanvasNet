using System.Numerics;
using DemaConsulting.CanvasNet.Drawing;

// cspell:ignore Lerp

namespace DemaConsulting.CanvasNet.Tests.Drawing;

/// <summary>
///     Unit tests for the internal <see cref="DashSplitter"/> type.
/// </summary>
public class DashSplitterTests
{
    /// <summary>
    ///     Proves that a null or empty dash array leaves the input polyline unchanged.
    /// </summary>
    [Fact]
    public void DashSplitter_Split_NullOrEmptyDashArray_ReturnsWholeSegmentUnchanged()
    {
        // Arrange
        var points = new List<Vector2> { new(0, 0), new(10, 0) };

        // Act
        var nullDash = DashSplitter.Split(points, isClosed: false, dashArray: null, dashOffset: 0f);
        var emptyDash = DashSplitter.Split(points, isClosed: false, dashArray: Array.Empty<float>(), dashOffset: 0f);

        // Assert
        Assert.Equal(points, Assert.Single(nullDash).Points);
        Assert.Equal(points, Assert.Single(emptyDash).Points);
    }

    /// <summary>
    ///     Proves that an odd-length dash array is conceptually duplicated.
    /// </summary>
    [Fact]
    public void DashSplitter_Split_OddLengthDashArray_DuplicatesArrayConceptually()
    {
        // Arrange
        var points = new List<Vector2> { new(0, 0), new(10, 0) };

        // Act
        var segments = DashSplitter.Split(points, isClosed: false, dashArray: [2f, 1f, 3f], dashOffset: 0f);

        // Assert
        Assert.Equal(3, segments.Count);
        Assert.Equal([new Vector2(0, 0), new Vector2(2, 0)], segments[0].Points);
        Assert.Equal([new Vector2(3, 0), new Vector2(6, 0)], segments[1].Points);
        Assert.Equal([new Vector2(8, 0), new Vector2(9, 0)], segments[2].Points);
    }

    /// <summary>
    ///     Proves that dash offset phase-shifts the first visible segment.
    /// </summary>
    [Fact]
    public void DashSplitter_Split_WithDashOffset_ShiftsPhaseOfFirstSegment()
    {
        // Arrange
        var points = new List<Vector2> { new(0, 0), new(10, 0) };

        // Act
        var segments = DashSplitter.Split(points, isClosed: false, dashArray: [4f, 2f], dashOffset: 1f);

        // Assert
        Assert.Equal(2, segments.Count);
        Assert.Equal([new Vector2(0, 0), new Vector2(3, 0)], segments[0].Points);
        Assert.Equal([new Vector2(5, 0), new Vector2(9, 0)], segments[1].Points);
    }

    /// <summary>
    ///     Proves that a visible closed-path dash segment wrapping across the seam is stitched
    ///     into one emitted segment.
    /// </summary>
    [Fact]
    public void DashSplitter_Split_ClosedPolylineDashWrappingSeam_StitchesSegmentAcrossStartPoint()
    {
        // Arrange
        var points = new List<Vector2>
        {
            new(0, 0),
            new(2, 0),
            new(2, 2),
            new(0, 2)
        };

        // Act
        var segments = DashSplitter.Split(points, isClosed: true, dashArray: [3f, 5f], dashOffset: 2f);

        // Assert
        var segment = Assert.Single(segments);
        Assert.False(segment.IsClosed);
        Assert.Equal([new Vector2(0, 2), new Vector2(0, 0), new Vector2(1, 0)], segment.Points);
    }

    /// <summary>
    ///     Proves that a dash pattern longer than the polyline emits one partial "on" segment
    ///     when the path ends mid-dash.
    /// </summary>
    [Fact]
    public void DashSplitter_Split_PatternLongerThanPolyline_ReturnsSinglePartialOnSegment()
    {
        // Arrange
        var points = new List<Vector2> { new(0, 0), new(4, 0) };

        // Act
        var segments = DashSplitter.Split(points, isClosed: false, dashArray: [10f, 5f], dashOffset: 0f);

        // Assert
        Assert.Equal([new Vector2(0, 0), new Vector2(4, 0)], Assert.Single(segments).Points);
    }

    /// <summary>
    ///     Proves that a dash array whose entries are each individually finite but whose summed
    ///     total pattern length overflows a naive float32 accumulation (e.g. two
    ///     <see cref="float.MaxValue"/> entries) completes without hanging, even when combined
    ///     with a negative dash offset that forces phase normalization to traverse the pattern.
    ///     Prior to the fix, the overflowed (<see cref="float.PositiveInfinity"/>) pattern length
    ///     made the phase-traversal loop subtract finite dash entries from infinity forever - an
    ///     effectively infinite loop. This test intentionally makes no timing assertion: it simply
    ///     relies on xUnit's normal test execution completing at all to prove the loop terminates.
    /// </summary>
    [Fact]
    public void DashSplitter_Split_OverflowProneDashArrayWithNegativeOffset_CompletesWithoutHanging()
    {
        // Arrange: a dash pattern whose two entries individually are finite but whose float32 sum
        // overflows to +Infinity, paired with a negative dash offset (both independently legal
        // per StrokeStyle's validation) so phase normalization must traverse the pattern.
        var points = new List<Vector2> { new(0, 0), new(10, 0) };

        // Act
        var segments = DashSplitter.Split(
            points,
            isClosed: false,
            dashArray: [float.MaxValue, float.MaxValue],
            dashOffset: -1f);

        // Assert: the call returned (did not hang) with a well-formed result
        Assert.NotNull(segments);
    }

    /// <summary>
    ///     Proves that a small negative dash offset against an astronomically large dash pattern
    ///     produces the CORRECT phase (not merely a non-hanging result): the offset must land in
    ///     the final unit of the preceding "off" entry rather than being silently swallowed by
    ///     catastrophic cancellation and snapping back to the start of the "on" entry.
    /// </summary>
    /// <remarks>
    ///     With dash pattern <c>[float.MaxValue, float.MaxValue]</c> (an "on" entry followed by an
    ///     "off" entry, each individually finite but summing to a pattern length only
    ///     representable in <see langword="double"/>) and <c>dashOffset: -1f</c>, naively
    ///     normalizing the offset via <c>modulus + (-1)</c> in <see langword="double"/> rounds
    ///     straight back to <c>modulus</c> - the tiny <c>-1</c> is swallowed because doubles have
    ///     roughly 16 significant decimal digits and the modulus has 39. That silently moves the
    ///     phase to the start of the first ("on") entry instead of one unit before the end of the
    ///     second ("off") entry. The correct phase is one unit into the path's "off" span, so only
    ///     the remaining 9 units of the 10-unit path are "on".
    /// </remarks>
    [Fact]
    public void DashSplitter_Split_TinyNegativeOffsetAgainstHugePattern_ProducesCorrectPhase()
    {
        // Arrange
        var points = new List<Vector2> { new(0, 0), new(10, 0) };

        // Act
        var segments = DashSplitter.Split(
            points,
            isClosed: false,
            dashArray: [float.MaxValue, float.MaxValue],
            dashOffset: -1f);

        // Assert: the first 1 unit is the tail of the "off" entry (invisible); the remaining 9
        // units are the following "on" entry.
        var segment = Assert.Single(segments);
        Assert.Equal([new Vector2(1, 0), new Vector2(10, 0)], segment.Points);
    }

    /// <summary>
    ///     Proves correct phase resolution for a second extreme-magnitude-plus-small-negative-
    ///     offset combination, this time with an asymmetric pattern (a small "on" entry paired
    ///     with an astronomically large "off" entry).
    /// </summary>
    [Fact]
    public void DashSplitter_Split_TinyNegativeOffsetAgainstAsymmetricHugePattern_ProducesCorrectPhase()
    {
        // Arrange: pattern [5 (on), float.MaxValue (off)], offset -2 lands 2 units before the end
        // of the huge "off" entry, so the path starts 2 units into "off", then 5 units "on", then
        // back into the (still enormous) "off" entry for the remainder of the 10-unit path.
        var points = new List<Vector2> { new(0, 0), new(10, 0) };

        // Act
        var segments = DashSplitter.Split(
            points,
            isClosed: false,
            dashArray: [5f, float.MaxValue],
            dashOffset: -2f);

        // Assert
        var segment = Assert.Single(segments);
        Assert.Equal([new Vector2(2, 0), new Vector2(7, 0)], segment.Points);
    }

    /// <summary>
    ///     Proves that an all-zero dash array is treated as a solid stroke.
    /// </summary>
    [Fact]
    public void DashSplitter_Split_AllZeroDashArray_TreatedAsSolid()
    {
        // Arrange
        var points = new List<Vector2> { new(0, 0), new(6, 0) };

        // Act
        var segments = DashSplitter.Split(points, isClosed: false, dashArray: [0f, 0f, 0f], dashOffset: 5f);

        // Assert
        Assert.Equal(points, Assert.Single(segments).Points);
    }

    /// <summary>
    ///     Proves that a dash pattern beginning with a zero-length "on" entry, evaluated on a
    ///     zero-length path, correctly starts in the following "off" entry rather than staying in
    ///     the zero-length "on" entry.
    /// </summary>
    /// <remarks>
    ///     With dash pattern <c>[0f, 2f]</c> (a zero-length "on" entry followed by a 2-length
    ///     "off" entry) and the default zero dash offset, the phase-traversal loop in
    ///     <c>IsDashOnAtStart</c> normalizes to an offset of exactly zero, which - prior to the
    ///     fix - skipped the traversal loop entirely and left the phase index at 0 (the "on"
    ///     entry), even though a zero-length "on" entry has no visible extent and phase zero has
    ///     therefore already moved into the following "off" entry. A zero-length path (here, a
    ///     single point, which collapses to the <c>edgeCount &lt;= 0</c> special case in
    ///     <c>Split</c>) makes this phase-only decision the entire result: the path is either
    ///     emitted whole (if the phase starts "on") or produces no segments at all (if "off").
    ///     The correct "off" phase must therefore produce zero segments.
    /// </remarks>
    [Fact]
    public void DashSplitter_Split_ZeroLengthLeadingDashEntryOnZeroLengthPath_StartsInFollowingOffEntry()
    {
        // Arrange: a single-point (zero-length) open subpath
        var points = new List<Vector2> { new(2, 2) };

        // Act
        var segments = DashSplitter.Split(points, isClosed: false, dashArray: [0f, 2f], dashOffset: 0f);

        // Assert: the phase starts in the 2-length "off" entry following the zero-length "on"
        // entry, so no segments are emitted
        Assert.Empty(segments);
    }

    /// <summary>
    ///     Proves that a long path (millions of units) combined with a fine dash pattern
    ///     completes and produces numerically correct dash intervals, rather than hanging.
    /// </summary>
    /// <remarks>
    ///     Prior to the fix, the path-length accumulator (<c>position</c>) in
    ///     <c>BuildOnIntervals</c> was a <see langword="float"/>. At a magnitude of roughly
    ///     17,000,000 units, float32's ULP (unit in the last place) grows to about 2, which is
    ///     larger than the 1-unit dash span in the <c>[1, 1]</c> pattern used here: adding a
    ///     1-unit span to <c>position</c> would round straight back to the same value, so the
    ///     loop's exit condition (<c>position &lt; totalLength</c>) was never reached - an
    ///     unconditional infinite loop, not merely a slow one. This test makes no timing
    ///     assertion (matching <see cref="DashSplitter_Split_OverflowProneDashArrayWithNegativeOffset_CompletesWithoutHanging"/>
    ///     above): it relies on the call actually returning at all to prove the loop terminates.
    ///     The path is built from unit-length edges (rather than one giant edge) so that a sample
    ///     of segments comfortably below float32's ~8,388,608 exact-integer boundary can be
    ///     asserted at their bit-exact expected positions: with a single giant edge, interpolating
    ///     a point uses <c>t * hugeEdgeLength</c>, which amplifies float32's relative rounding
    ///     error in <c>t</c> into an absolute error of roughly half a unit even for "small"
    ///     positions - an artifact of that interpolation shape, unrelated to the accumulator fix
    ///     under test here. Interpolating within short, unit-length edges avoids that amplification
    ///     entirely.
    /// </remarks>
    [Fact]
    public void DashSplitter_Split_FineDashPatternOnVeryLongPath_CompletesWithCorrectSegments()
    {
        // Arrange: a 17,000,000-unit straight path built from unit-length edges (so per-edge
        // interpolation stays precise), comfortably past float32's ~16,777,216-unit
        // exact-integer boundary, with a fine 1-on/1-off dash pattern.
        const int length = 17_000_000;
        var points = new List<Vector2>(length + 1);
        for (var i = 0; i <= length; i++)
        {
            points.Add(new Vector2(i, 0));
        }

        // Act
        var segments = DashSplitter.Split(points, isClosed: false, dashArray: [1f, 1f], dashOffset: 0f);

        // Assert: the call returned (did not hang) with exactly the expected number of "on"
        // segments - itself strong evidence the interval-building loop correctly advanced across
        // the whole path instead of stalling. The first segment and a segment comfortably below
        // float32's 2^23 integer-precision boundary are asserted at their exact expected
        // positions; the very last segment sits above that boundary, where Vector2's float32
        // coordinates cannot represent every unit integer exactly (a pre-existing, unrelated
        // limitation of the public Vector2-based API, not of the dash-interval math itself under
        // test here), so only a loose positional sanity check is made for it.
        Assert.Equal(length / 2, segments.Count);
        Assert.Equal([new Vector2(0, 0), new Vector2(1, 0)], segments[0].Points);
        Assert.Equal(
            [new Vector2(6_000_000, 0), new Vector2(6_000_001, 0)],
            segments[3_000_000].Points);
        Assert.True(segments[^1].Points[0].X >= length - 10f);
    }

    /// <summary>
    ///     Proves that a huge-but-finite total path length combined with a fine dash span (the
    ///     combination that previously made <c>BuildOnIntervals</c>'s traversal loop require an
    ///     impractical number of iterations to finish) falls back to a solid stroke instead of
    ///     consuming disproportionate CPU time.
    /// </summary>
    /// <remarks>
    ///     With endpoints at <c>(-1e20, -1e20)</c> and <c>(1e20, 1e20)</c>, the path length is
    ///     roughly <c>2.83e20</c> - finite in double precision, but at that magnitude double's ULP
    ///     (unit in the last place) is far larger than the 5-unit dash span in <c>[5, 5]</c>, so
    ///     <c>totalLength / dashSpan</c> alone is an impractically large ratio, independent of the
    ///     loop's separate <see cref="Math.BitIncrement(double)"/>-based defensive advance-guard
    ///     (which exists for termination-safety at even larger magnitudes, but is not what makes
    ///     this specific input slow). This test proves the fix: <c>BuildOnIntervals</c>'s cheap
    ///     pre-flight iteration estimate detects that this input's cost would exceed the iteration
    ///     budget and short-circuits straight to the fallback - without ever running the traversal
    ///     loop - so dashing is abandoned entirely for the whole path and a solid stroke (a single
    ///     segment containing the original points, unchanged) is returned instead, mirroring this
    ///     codebase's other "cannot use this dash pattern -&gt; solid stroke" fallbacks.
    ///     <para>
    ///     Calls <see cref="DashSplitter.Split"/> directly and synchronously (no
    ///     <c>Task.Run</c>/<c>Task.WhenAny</c>/<c>Task.Delay</c> race): the pre-flight
    ///     short-circuit makes this a deterministic, hardware-independent, effectively O(1)
    ///     computation - the same kind of guarantee already exercised by this test class's many
    ///     other fixed budget/cap tests (e.g.
    ///     <see cref="DashSplitter_Split_OrdinaryDashPatternOnOrdinaryPath_ProducesNormalDashSegments"/>
    ///     immediately below) - so the correct regression signal is that the call returns at all
    ///     with the documented fallback shape, not how long it takes on any given machine. No
    ///     timing-based assertion is made here, consistent with this project's policy against
    ///     elapsed-time-based test assertions.
    ///     </para>
    /// </remarks>
    [Fact]
    public void DashSplitter_Split_HugeFiniteTotalLengthWithFineDashSpan_FallsBackToSolidStroke()
    {
        // Arrange: an edge whose length is huge but finite, paired with a fine dash span whose
        // resolution is far below double's ULP at that magnitude.
        var points = new List<Vector2> { new(-1e20f, -1e20f), new(1e20f, 1e20f) };

        // Act: direct, synchronous call - the pre-flight iteration-budget estimate (not
        // wall-clock time) is what guarantees termination, so there is nothing to race against.
        var segments = DashSplitter.Split(points, isClosed: false, dashArray: [5f, 5f], dashOffset: 0f);

        // Assert: the call completed (did not hang) - dashing was abandoned entirely, and the
        // whole path is emitted unchanged as a single (unclosed) segment, exactly as the other
        // "cannot use this dash pattern" fallbacks above already behave.
        Assert.Equal(points, Assert.Single(segments).Points);
    }

    /// <summary>
    ///     Regression test for a cloud-PR-review-confirmed gap: a dash pattern whose iteration
    ///     count stays at (not above) <c>MaxOnIntervalIterations</c> can still retain far more
    ///     "on" intervals than <c>MaxOnIntervalCount</c> permits, and must fall back to a solid
    ///     stroke instead of materializing millions of retained intervals/segments.
    /// </summary>
    /// <remarks>
    ///     What is <i>counted</i> by <c>MaxOnIntervalIterations</c> (loop iterations - two per
    ///     dash-pattern-entry transition) is a different quantity than what is <i>retained</i> in
    ///     memory (only the strictly-positive, even-indexed "on" entries append an interval). For
    ///     a two-point, 50,000,000-unit path with a <c>[1, 1]</c> pattern:
    ///     <c>estimatedIterations = 2 * 2 * (50,000,000 / 2) = 100,000,000</c>, which is <i>not
    ///     greater than</i> the 100,000,000-iteration cap - so <c>MaxOnIntervalIterations</c> alone
    ///     does not trigger the fallback - yet the same input retains
    ///     <c>estimatedOnIntervalCount = 1 * (50,000,000 / 2) = 25,000,000</c> on-intervals, far
    ///     above the 10,000,000 <c>MaxOnIntervalCount</c> cap. This test proves the new,
    ///     independent <c>MaxOnIntervalCount</c> pre-flight check catches exactly this case and
    ///     falls back to a solid stroke (a single unchanged segment), the same fallback shape used
    ///     by <see cref="DashSplitter_Split_HugeFiniteTotalLengthWithFineDashSpan_FallsBackToSolidStroke"/>
    ///     above. Uses a single giant edge (rather than per-unit vertices, matching that test's
    ///     "single giant edge, no per-point materialization" technique) so the test itself stays
    ///     fast and allocation-free regardless of the pathological on-interval count it is proving
    ///     is rejected before ever being materialized.
    ///     <para>
    ///     Calls <see cref="DashSplitter.Split"/> directly and synchronously (no
    ///     <c>Task.Run</c>/<c>Task.WhenAny</c>/<c>Task.Delay</c> race): the pre-flight
    ///     short-circuit makes this a deterministic, hardware-independent, effectively O(1)
    ///     computation, so the correct regression signal is that the call returns at all with the
    ///     documented fallback shape, not how long it takes on any given machine. No timing-based
    ///     assertion is made here, consistent with this project's policy against elapsed-time-based
    ///     test assertions.
    ///     </para>
    /// </remarks>
    [Fact]
    public void DashSplitter_Split_ManyRetainedOnIntervalsWithinIterationBudget_FallsBackToSolidStroke()
    {
        // Arrange: a single 50,000,000-unit edge (no per-point materialization) with a fine
        // [1, 1] dash pattern - within MaxOnIntervalIterations' budget, but far above
        // MaxOnIntervalCount's retained-on-interval budget.
        var points = new List<Vector2> { new(0f, 0f), new(50_000_000f, 0f) };

        // Act: direct, synchronous call - the pre-flight on-interval-count budget estimate (not
        // wall-clock time) is what guarantees termination without materializing 25,000,000
        // intervals, so there is nothing to race against.
        var segments = DashSplitter.Split(points, isClosed: false, dashArray: [1f, 1f], dashOffset: 0f);

        // Assert: the call completed (did not hang/OOM) - dashing was abandoned entirely, and the
        // whole path is emitted unchanged as a single (unclosed) segment, exactly as the other
        // "cannot use this dash pattern" fallbacks above already behave.
        Assert.Equal(points, Assert.Single(segments).Points);
    }

    /// <summary>
    ///     Regression test for a quality-review-confirmed pre-flight-formula bug: an
    ///     <b>asymmetric</b> dash pattern (one small entry mixed with a much larger one) combined
    ///     with a long-but-ordinary path must still produce genuine dashed output, not the
    ///     solid-stroke fallback.
    /// </summary>
    /// <remarks>
    ///     The pre-flight estimate's original formula, <c>2 * totalLength / minPositiveSpan</c>
    ///     (based only on the single smallest positive pattern entry), ignored the pattern's total
    ///     cycle length entirely. For this test's <c>[1, 1_000_000]</c> pattern over a
    ///     100,000,000-unit path, that buggy formula computed <c>2 * 1e8 / 1 = 2e8</c>, which
    ///     exceeds <c>MaxOnIntervalIterations</c> (100,000,000) and incorrectly short-circuited to
    ///     the solid-stroke fallback - even though the real traversal loop only needs about 399
    ///     iterations (roughly 100 pattern cycles) to resolve this input correctly. The corrected
    ///     formula, <c>2 * positiveEntryCount * (totalLength / patternLength)</c>
    ///     (<c>2 * 2 * (1e8 / 1_000_001) &#8776; 400</c>), stays comfortably under the cap and lets
    ///     the real loop run to completion, producing exactly 100 one-unit "on" segments (verified
    ///     by direct simulation of the traversal loop's exact cost model). This directly contrasts
    ///     with <see cref="DashSplitter_Split_HugeFiniteTotalLengthWithFineDashSpan_FallsBackToSolidStroke"/>
    ///     immediately above, which uses a symmetric <c>[5, 5]</c> pattern that genuinely does
    ///     require an impractical iteration count and must still correctly fall back to a solid
    ///     stroke - proving the fix distinguishes the two cases rather than merely disabling the
    ///     short-circuit altogether.
    /// </remarks>
    [Fact]
    public void DashSplitter_Split_AsymmetricDashPatternOnLongPath_ProducesGenuineDashSegments()
    {
        // Arrange: a 100,000,000-unit straight path (well within a realistic path length) with an
        // asymmetric [1, 1_000_000] dash pattern - a tiny "on" span followed by a huge "off" span.
        var points = new List<Vector2> { new(0f, 0f), new(1e8f, 0f) };

        // Act
        var segments = DashSplitter.Split(points, isClosed: false, dashArray: [1f, 1_000_000f], dashOffset: 0f);

        // Assert: genuine dashing occurred - exactly the 100 small "on" segments the traversal
        // loop's cost model predicts (independently verified by direct simulation of the loop),
        // not the single whole-path solid-stroke fallback segment the pre-flight bug incorrectly
        // produced. The segment count alone is decisive evidence of genuine dashing vs. fallback:
        // the buggy formula collapsed this input to exactly one segment spanning the whole path.
        Assert.Equal(100, segments.Count);
        Assert.DoesNotContain(segments, segment => segment.Points.SequenceEqual(points));
    }

    /// <summary>
    ///     Proves that an ordinary dash pattern on an ordinary-length path is entirely unaffected
    ///     by the new iteration-budget cap: it neither triggers the solid-stroke fallback nor
    ///     changes the emitted dash segments compared to pre-fix behavior.
    /// </summary>
    [Fact]
    public void DashSplitter_Split_OrdinaryDashPatternOnOrdinaryPath_ProducesNormalDashSegments()
    {
        // Arrange: a simple 17-unit horizontal path with a [3, 2] dash pattern (well within any
        // reasonable iteration count, and far from any extreme-magnitude coordinate), long enough
        // to include a final partial "on" dash.
        var points = new List<Vector2> { new(0, 0), new(17, 0) };

        // Act
        var segments = DashSplitter.Split(points, isClosed: false, dashArray: [3f, 2f], dashOffset: 0f);

        // Assert: the expected three full "on" dashes plus one final partial dash, none of which
        // is the whole-path solid-stroke fallback shape.
        Assert.Equal(
            [
                new List<Vector2> { new(0, 0), new(3, 0) },
                new List<Vector2> { new(5, 0), new(8, 0) },
                new List<Vector2> { new(10, 0), new(13, 0) },
                new List<Vector2> { new(15, 0), new(17, 0) }
            ],
            segments.Select(s => s.Points).ToList());
    }

    /// <summary>
    ///     Proves that an edge spanning near-extreme float32 coordinates (from near
    ///     <see cref="float.MinValue"/> to near <see cref="float.MaxValue"/>) - a legal, finite
    ///     pair of <see cref="Vector2"/> endpoints - completes and produces finite dash intervals,
    ///     rather than hanging.
    /// </summary>
    /// <remarks>
    ///     Prior to the fix, per-edge length in <c>BuildCumulativeLengths</c> was computed via
    ///     <see cref="Vector2.Distance"/>, which internally computes <c>dx*dx + dy*dy</c> in
    ///     float32 before taking the square root. For this edge, that intermediate squaring
    ///     overflows float32's representable range and produces <see cref="float.PositiveInfinity"/>,
    ///     even though the true distance - while enormous - is finite in double precision. That
    ///     infinite edge length made <c>totalLength</c> infinite, and the traversal loop in
    ///     <c>BuildOnIntervals</c> (<c>while (position &lt; totalLength)</c>) could never
    ///     terminate, since <c>position</c> can never reach infinity. This test makes no timing
    ///     assertion (matching the other hang-regression tests above): it relies on the call
    ///     actually returning at all to prove the loop terminates, and additionally asserts the
    ///     produced segment endpoints are finite.
    /// </remarks>
    [Fact]
    public void DashSplitter_Split_EdgeSpanningExtremeFloat32Coordinates_CompletesWithFiniteSegments()
    {
        // Arrange: a single edge from near float.MinValue to near float.MaxValue. Vector2.Distance
        // computes dx*dx + dy*dy in float32, which overflows to +Infinity for this edge even
        // though the true (double-precision) distance (~3.4e38) is finite. A dash pattern scaled
        // to match that magnitude (rather than a fine [1, 1] pattern) keeps the number of
        // traversal-loop iterations small, so the test exercises only the length computation
        // itself rather than depending on the loop's (separately tested) large-iteration-count
        // behavior.
        var points = new List<Vector2>
        {
            new(float.MinValue / 2, 0),
            new(float.MaxValue / 2, 0)
        };

        // Act
        var segments = DashSplitter.Split(points, isClosed: false, dashArray: [1e38f, 1e38f], dashOffset: 0f);

        // Assert: the call returned (did not hang), and every emitted point is finite.
        Assert.NotEmpty(segments);
        foreach (var segment in segments)
        {
            foreach (var point in segment.Points)
            {
                Assert.True(float.IsFinite(point.X));
                Assert.True(float.IsFinite(point.Y));
            }
        }
    }

    /// <summary>
    ///     Proves that a dash interval boundary landing strictly inside an edge spanning extreme
    ///     float32 coordinates (from <c>-<see cref="float.MaxValue"/></c> to
    ///     <see cref="float.MaxValue"/>) produces a FINITE, numerically reasonable interpolated
    ///     point, rather than a point with <see cref="float.PositiveInfinity"/>/<c>NaN</c>
    ///     coordinates.
    /// </summary>
    /// <remarks>
    ///     <see cref="Vector2.Lerp(Vector2, Vector2, float)"/>'s documented contract computes
    ///     <c>start + (end - start) * t</c> with the <c>end - start</c> subtraction performed on
    ///     float32 endpoints: for this edge, that subtraction is
    ///     <c>float.MaxValue - (-float.MaxValue)</c>, which overflows float32's representable
    ///     range to <see cref="float.PositiveInfinity"/>, even though the true (double-precision)
    ///     delta - <c>2 * float.MaxValue</c> - is finite. Relying on
    ///     <see cref="Vector2.Lerp(Vector2, Vector2, float)"/> for this step would therefore be
    ///     fragile: its documented formula would poison the interpolated point with
    ///     <c>Infinity</c>/<c>NaN</c> for this edge if evaluated literally as documented, even
    ///     though the edge itself is entirely legal and finite (whether or not a given runtime's
    ///     internal implementation happens to avoid that in practice is not part of its documented
    ///     contract). Interpolating each coordinate explicitly in <see langword="double"/>
    ///     precision before narrowing back to <see langword="float"/> guarantees correctness
    ///     regardless of that implementation detail.
    /// </remarks>
    [Fact]
    public void DashSplitter_Split_DashBoundaryInsideExtremeFloat32Edge_ProducesFinitePoint()
    {
        // Arrange: a single edge from -float.MaxValue to float.MaxValue (length ~6.8e38, finite in
        // double precision). A dash pattern of [1e38 (on), 1e38 (off)] places the first "on"/"off"
        // boundary at distance 1e38, which - since the edge is ~6.8e38 long - falls strictly
        // inside the edge, forcing GetPointAtDistance to interpolate an interior point rather than
        // simply returning one of the edge's own endpoints.
        var points = new List<Vector2> { new(-float.MaxValue, 0f), new(float.MaxValue, 0f) };

        // Act
        var segments = DashSplitter.Split(points, isClosed: false, dashArray: [1e38f, 1e38f], dashOffset: 0f);

        // Assert: the first "on" segment runs from the edge start to the first dash boundary at
        // distance 1e38 (strictly inside the edge), so its final point is the interpolated one.
        var segment = Assert.Single(segments, s => s.Points[0] == points[0]);
        var interpolated = segment.Points[^1];
        Assert.True(float.IsFinite(interpolated.X), $"Expected finite X but got {interpolated.X}");
        Assert.True(float.IsFinite(interpolated.Y), $"Expected finite Y but got {interpolated.Y}");

        const double edgeLength = 2d * float.MaxValue;
        const double expectedT = 1e38d / edgeLength;
        var expectedX = -(double)float.MaxValue + edgeLength * expectedT;
        Assert.Equal(expectedX, interpolated.X, Math.Abs(expectedX) * 1e-5);
        Assert.Equal(0f, interpolated.Y);
    }
}
