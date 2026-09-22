// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO
using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Parses a font's <c>loca</c> (glyph location) table and decodes individual glyph outlines
///     from the <c>glyf</c> table into <see cref="Path"/> geometry, including composite glyph
///     resolution.
/// </summary>
/// <remarks>
///     <para>
///     <c>loca</c> is parsed eagerly at construction time (validating it is monotonically
///     non-decreasing and consistent with <c>glyf</c>'s length), but no individual glyph's
///     contour data is decoded until <see cref="GetGlyphOutline"/> is called for that specific
///     index - one corrupt glyph's contour data does not prevent using every other, otherwise
///     well-formed, glyph in the font.
///     </para>
///     <para>
///     Composite glyph resolution is bounded by two independent, deterministic counters (never a
///     timer): a nesting depth cap of <see cref="MaxDepth"/> and a total resolved component count
///     cap of <see cref="MaxTotalComponents"/> across the whole <see cref="GetGlyphOutline"/>
///     call. A depth cap alone would not prevent exponential blow-up from non-cyclic nested
///     composites; the component-count cap is the more important of the two bounds. Point-matched
///     composite components (<c>ARGS_ARE_XY_VALUES</c> clear) are rejected with
///     <see cref="InvalidDataException"/> rather than silently mis-positioned.
///     </para>
/// </remarks>
internal sealed class GlyfLocaReader
{
    /// <summary>
    ///     The maximum composite glyph nesting depth permitted before
    ///     <see cref="InvalidDataException"/> is thrown (also bounds true composite cycles, which
    ///     always eventually exceed this depth).
    /// </summary>
    private const int MaxDepth = 10;

    /// <summary>
    ///     The maximum total number of resolved glyph/component visits permitted across a single
    ///     <see cref="GetGlyphOutline"/> call, bounding non-cyclic exponential composite blow-up.
    /// </summary>
    private const int MaxTotalComponents = 5000;

    private const int ArgsAreWords = 0x0001;
    private const int ArgsAreXyValues = 0x0002;
    private const int WeHaveAScale = 0x0008;
    private const int MoreComponents = 0x0020;
    private const int WeHaveAnXAndYScale = 0x0040;
    private const int WeHaveATwoByTwo = 0x0080;

    private readonly byte[] _data;
    private readonly int _glyfOffset;
    private readonly int _glyfLength;
    private readonly int[] _locaOffsets;

    /// <summary>
    ///     The total number of glyphs described by <c>loca</c> (one less than the number of
    ///     entries in <see cref="_locaOffsets"/>).
    /// </summary>
    public int GlyphCount { get; }

    private GlyfLocaReader(byte[] data, int glyfOffset, int glyfLength, int[] locaOffsets, int glyphCount)
    {
        _data = data;
        _glyfOffset = glyfOffset;
        _glyfLength = glyfLength;
        _locaOffsets = locaOffsets;
        GlyphCount = glyphCount;
    }

    /// <summary>
    ///     Parses the <c>loca</c> table.
    /// </summary>
    /// <param name="data">The complete font file contents.</param>
    /// <param name="locaOffset">The offset of the <c>loca</c> table.</param>
    /// <param name="locaLength">The length of the <c>loca</c> table.</param>
    /// <param name="glyfOffset">The offset of the <c>glyf</c> table.</param>
    /// <param name="glyfLength">The length of the <c>glyf</c> table.</param>
    /// <param name="numGlyphs">The font's total glyph count, from <c>maxp</c>.</param>
    /// <param name="longFormat">
    ///     <see langword="true"/> to parse 32-bit (<c>Offset32</c>) <c>loca</c> entries; <see langword="false"/>
    ///     for 16-bit (<c>Offset16</c>, doubled) entries - selected by <c>head.indexToLocFormat</c>.
    /// </param>
    /// <returns>A new <see cref="GlyfLocaReader"/> exposing the parsed glyph location table.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>loca</c> is too short for its declared entry count, when any entry
    ///     exceeds the bounds of the <c>glyf</c> table, or when entries are not monotonically
    ///     non-decreasing.
    /// </exception>
    public static GlyfLocaReader Parse(byte[] data, int locaOffset, int locaLength, int glyfOffset, int glyfLength, int numGlyphs, bool longFormat)
    {
        var count = numGlyphs + 1;
        var entrySize = longFormat ? 4 : 2;
        var required = (long)count * entrySize;
        if (required > locaLength)
        {
            throw new InvalidDataException("The 'loca' table is too short for the declared glyph count.");
        }

        var offsets = new int[count];
        for (var i = 0; i < count; i++)
        {
            long raw = longFormat
                ? SfntContainer.ReadUInt32(data, locaOffset + i * 4)
                : (long)SfntContainer.ReadUInt16(data, locaOffset + i * 2) * 2;

            if (raw > glyfLength)
            {
                throw new InvalidDataException("A 'loca' entry exceeds the bounds of the 'glyf' table.");
            }

            if (i > 0 && raw < offsets[i - 1])
            {
                throw new InvalidDataException("The 'loca' table entries must be monotonically non-decreasing.");
            }

            offsets[i] = (int)raw;
        }

        return new GlyfLocaReader(data, glyfOffset, glyfLength, offsets, numGlyphs);
    }

    /// <summary>
    ///     Decodes a single glyph's outline.
    /// </summary>
    /// <param name="glyphIndex">The glyph index to decode.</param>
    /// <returns>
    ///     The glyph's outline in raw font design units, or <see cref="Path.Empty"/> for a glyph
    ///     with no contour data (for example <c>space</c>).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="glyphIndex"/> is outside <c>[0, GlyphCount)</c>.
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the glyph's contour data is malformed or truncated, when a composite
    ///     glyph's nesting depth or total resolved component count exceeds its bound, or when a
    ///     composite glyph contains a point-matched component or references an out-of-range
    ///     component glyph index.
    /// </exception>
    public Path GetGlyphOutline(int glyphIndex)
    {
        if (glyphIndex < 0 || glyphIndex >= GlyphCount)
        {
            throw new ArgumentOutOfRangeException(nameof(glyphIndex), glyphIndex, "Glyph index is out of range.");
        }

        var totalComponents = 0;
        return DecodeGlyph(glyphIndex, 0, ref totalComponents);
    }

    /// <summary>
    ///     Decodes a single glyph (simple or composite), enforcing the depth and total-component
    ///     bounds.
    /// </summary>
    private Path DecodeGlyph(int glyphIndex, int depth, ref int totalComponents)
    {
        if (glyphIndex < 0 || glyphIndex >= GlyphCount)
        {
            throw new InvalidDataException("Composite glyph references an out-of-range component glyph index.");
        }

        if (depth > MaxDepth)
        {
            throw new InvalidDataException("Composite glyph nesting exceeds the supported depth.");
        }

        totalComponents++;
        if (totalComponents > MaxTotalComponents)
        {
            throw new InvalidDataException("Composite glyph resolves to too many total components.");
        }

        var start = _locaOffsets[glyphIndex];
        var end = _locaOffsets[glyphIndex + 1];
        if (start == end)
        {
            return Path.Empty;
        }

        var glyphOffset = _glyfOffset + start;
        var glyphLength = end - start;
        if (glyphLength < 10 || end > _glyfLength)
        {
            throw new InvalidDataException("Glyph data is too short to contain a glyph header.");
        }

        var numberOfContours = SfntContainer.ReadInt16(_data, glyphOffset);
        if (numberOfContours >= 0)
        {
            return DecodeSimpleGlyph(glyphOffset, glyphLength, numberOfContours);
        }

        if (numberOfContours == -1)
        {
            return DecodeCompositeGlyph(glyphOffset, glyphLength, depth + 1, ref totalComponents);
        }

        throw new InvalidDataException("Glyph declares an invalid contour count.");
    }

    /// <summary>
    ///     Decodes a simple glyph's contour data into a <see cref="Path"/>.
    /// </summary>
    private Path DecodeSimpleGlyph(int glyphOffset, int glyphLength, int numberOfContours)
    {
        var limit = glyphOffset + glyphLength;
        var pos = glyphOffset + 10;

        if (numberOfContours == 0)
        {
            return Path.Empty;
        }

        EnsureAvailable(pos, numberOfContours * 2, limit);
        var endPts = new int[numberOfContours];
        var previousEnd = -1;
        for (var i = 0; i < numberOfContours; i++)
        {
            var value = SfntContainer.ReadUInt16(_data, pos);
            pos += 2;
            if (value <= previousEnd)
            {
                throw new InvalidDataException("Glyph contour end points must be strictly increasing.");
            }

            endPts[i] = value;
            previousEnd = value;
        }

        var numPoints = endPts[^1] + 1;

        EnsureAvailable(pos, 2, limit);
        var instructionLength = SfntContainer.ReadUInt16(_data, pos);
        pos += 2;
        EnsureAvailable(pos, instructionLength, limit);
        pos += instructionLength;

        var flags = new byte[numPoints];
        var i2 = 0;
        while (i2 < numPoints)
        {
            EnsureAvailable(pos, 1, limit);
            var flag = _data[pos++];
            flags[i2++] = flag;
            if ((flag & 0x08) != 0)
            {
                EnsureAvailable(pos, 1, limit);
                var repeatCount = _data[pos++];
                for (var r = 0; r < repeatCount && i2 < numPoints; r++)
                {
                    flags[i2++] = flag;
                }
            }
        }

        var xs = new int[numPoints];
        var x = 0;
        for (var i = 0; i < numPoints; i++)
        {
            var flag = flags[i];
            if ((flag & 0x02) != 0)
            {
                EnsureAvailable(pos, 1, limit);
                var dx = _data[pos++];
                x += (flag & 0x10) != 0 ? dx : -dx;
            }
            else if ((flag & 0x10) == 0)
            {
                EnsureAvailable(pos, 2, limit);
                x += SfntContainer.ReadInt16(_data, pos);
                pos += 2;
            }

            xs[i] = x;
        }

        var ys = new int[numPoints];
        var y = 0;
        for (var i = 0; i < numPoints; i++)
        {
            var flag = flags[i];
            if ((flag & 0x04) != 0)
            {
                EnsureAvailable(pos, 1, limit);
                var dy = _data[pos++];
                y += (flag & 0x20) != 0 ? dy : -dy;
            }
            else if ((flag & 0x20) == 0)
            {
                EnsureAvailable(pos, 2, limit);
                y += SfntContainer.ReadInt16(_data, pos);
                pos += 2;
            }

            ys[i] = y;
        }

        var builder = new PathBuilder();
        var contourStart = 0;
        for (var c = 0; c < numberOfContours; c++)
        {
            var contourEnd = endPts[c];
            var points = new List<(Vector2 Point, bool OnCurve)>(contourEnd - contourStart + 1);
            for (var p = contourStart; p <= contourEnd; p++)
            {
                points.Add((new Vector2(xs[p], ys[p]), (flags[p] & 0x01) != 0));
            }

            EmitContour(builder, points);
            contourStart = contourEnd + 1;
        }

        return builder.Build();
    }

    /// <summary>
    ///     Emits one contour's on-curve/off-curve points into <paramref name="builder"/> as
    ///     line/quadratic-Bezier commands, synthesizing the TrueType "implied on-curve midpoint"
    ///     for consecutive off-curve points and for a contour with no on-curve point at all.
    /// </summary>
    private static void EmitContour(PathBuilder builder, List<(Vector2 Point, bool OnCurve)> points)
    {
        var n = points.Count;
        if (n == 0)
        {
            return;
        }

        var firstOnCurveIndex = points.FindIndex(p => p.OnCurve);

        Vector2 startPoint;
        List<(Vector2 Point, bool OnCurve)> ordered;
        if (firstOnCurveIndex >= 0)
        {
            startPoint = points[firstOnCurveIndex].Point;
            ordered = new List<(Vector2, bool)>(n);
            for (var k = 0; k < n; k++)
            {
                ordered.Add(points[(firstOnCurveIndex + 1 + k) % n]);
            }
        }
        else
        {
            startPoint = Midpoint(points[0].Point, points[^1].Point);
            ordered = points;
        }

        builder.MoveTo(startPoint);

        Vector2? pendingControl = null;
        foreach (var (point, onCurve) in ordered)
        {
            if (onCurve)
            {
                if (pendingControl.HasValue)
                {
                    builder.QuadraticBezierTo(pendingControl.Value, point);
                    pendingControl = null;
                }
                else
                {
                    builder.LineTo(point);
                }
            }
            else
            {
                if (pendingControl.HasValue)
                {
                    var implied = Midpoint(pendingControl.Value, point);
                    builder.QuadraticBezierTo(pendingControl.Value, implied);
                }

                pendingControl = point;
            }
        }

        if (pendingControl.HasValue)
        {
            builder.QuadraticBezierTo(pendingControl.Value, startPoint);
        }

        builder.Close();
    }

    private static Vector2 Midpoint(Vector2 a, Vector2 b) => (a + b) / 2f;

    /// <summary>
    ///     Decodes a composite glyph, recursively resolving each component's glyph outline and
    ///     applying its offset/scale/2x2 transform.
    /// </summary>
    private Path DecodeCompositeGlyph(int glyphOffset, int glyphLength, int depth, ref int totalComponents)
    {
        var limit = glyphOffset + glyphLength;
        var pos = glyphOffset + 10;
        var builder = new PathBuilder();
        var hasContent = false;

        bool more;
        do
        {
            EnsureAvailable(pos, 4, limit);
            var flags = SfntContainer.ReadUInt16(_data, pos);
            var componentGlyphIndex = SfntContainer.ReadUInt16(_data, pos + 2);
            pos += 4;

            if ((flags & ArgsAreXyValues) == 0)
            {
                throw new InvalidDataException("Point-matched composite glyph components are not supported.");
            }

            float dx, dy;
            if ((flags & ArgsAreWords) != 0)
            {
                EnsureAvailable(pos, 4, limit);
                dx = SfntContainer.ReadInt16(_data, pos);
                dy = SfntContainer.ReadInt16(_data, pos + 2);
                pos += 4;
            }
            else
            {
                EnsureAvailable(pos, 2, limit);
                dx = unchecked((sbyte)_data[pos]);
                dy = unchecked((sbyte)_data[pos + 1]);
                pos += 2;
            }

            float a = 1f, b = 0f, c = 0f, d = 1f;
            if ((flags & WeHaveAScale) != 0)
            {
                EnsureAvailable(pos, 2, limit);
                a = d = SfntContainer.ReadF2Dot14(_data, pos);
                pos += 2;
            }
            else if ((flags & WeHaveAnXAndYScale) != 0)
            {
                EnsureAvailable(pos, 4, limit);
                a = SfntContainer.ReadF2Dot14(_data, pos);
                d = SfntContainer.ReadF2Dot14(_data, pos + 2);
                pos += 4;
            }
            else if ((flags & WeHaveATwoByTwo) != 0)
            {
                EnsureAvailable(pos, 8, limit);
                a = SfntContainer.ReadF2Dot14(_data, pos);
                b = SfntContainer.ReadF2Dot14(_data, pos + 2);
                c = SfntContainer.ReadF2Dot14(_data, pos + 4);
                d = SfntContainer.ReadF2Dot14(_data, pos + 6);
                pos += 8;
            }

            var componentPath = DecodeGlyph(componentGlyphIndex, depth, ref totalComponents);
            AppendTransformed(builder, componentPath, a, b, c, d, dx, dy);
            hasContent = true;

            more = (flags & MoreComponents) != 0;
        }
        while (more);

        return hasContent ? builder.Build() : Path.Empty;
    }

    /// <summary>
    ///     Re-issues every subpath of <paramref name="source"/> into <paramref name="builder"/>,
    ///     applying the given 2x2 matrix (<paramref name="a"/>/<paramref name="b"/>/<paramref name="c"/>/<paramref name="d"/>)
    ///     and translation (<paramref name="dx"/>/<paramref name="dy"/>) to every point.
    /// </summary>
    private static void AppendTransformed(PathBuilder builder, Path source, float a, float b, float c, float d, float dx, float dy)
    {
        Vector2 Transform(Vector2 p) => new(a * p.X + c * p.Y + dx, b * p.X + d * p.Y + dy);

        foreach (var subpath in source.Subpaths)
        {
            builder.MoveTo(Transform(subpath.Start));
            foreach (var command in subpath.Commands)
            {
                switch (command.Type)
                {
                    case PathCommandType.LineTo:
                        builder.LineTo(Transform(command.EndPoint));
                        break;

                    case PathCommandType.QuadraticBezierTo:
                        builder.QuadraticBezierTo(Transform(command.Control1), Transform(command.EndPoint));
                        break;

                    case PathCommandType.Close:
                        builder.Close();
                        break;

                    default:
                        // Glyph outlines produced by this reader only ever contain LineTo,
                        // QuadraticBezierTo, and Close commands.
                        throw new InvalidOperationException("Unexpected path command in a glyph outline.");
                }
            }
        }
    }

    /// <summary>
    ///     Validates that <paramref name="count"/> bytes starting at <paramref name="pos"/> lie
    ///     within <paramref name="limit"/>, throwing <see cref="InvalidDataException"/> otherwise.
    /// </summary>
    private static void EnsureAvailable(int pos, int count, int limit)
    {
        if (pos < 0 || count < 0 || (long)pos + count > limit)
        {
            throw new InvalidDataException("Glyph data is truncated or malformed.");
        }
    }
}
